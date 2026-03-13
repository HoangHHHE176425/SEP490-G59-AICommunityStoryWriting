using System.ClientModel;
using System.Text.Json;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Đồng sáng tác: Dàn ý (JSON) → Viết → Guardrail → Kiểm duyệt (JSON + violations) + vòng sửa. Constitutional rules trong prompt.</summary>
public class AICoCreationService : IAICoCreationService
{
    private const int DefaultMaxRevisions = 2;
    private const string ActionOutline = "CO_CREATE_OUTLINE";
    private const string ActionWrite = "CO_CREATE_WRITE";
    private const string ActionReview = "CO_CREATE_REVIEW";

    /// <summary>Quy tắc bắt buộc (Constitutional): đưa vào system prompt mọi agent.</summary>
    private const string ConstitutionalRules = """
Quy tắc bắt buộc: Bám sát thông tin trong ngữ cảnh (Story memory, Character/Event/Story State, RAG). Không viết ngược lại sự kiện đã nêu. Giữ đúng mạch truyện và logic cốt truyện của các chương trước; không thêm tình tiết mâu thuẫn hoặc lệch hướng. Chỉ trả về đúng định dạng yêu cầu, không thêm giải thích ngoài.
""";

    private readonly IStoryRepository _storyRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IAiGeneratedContentRepository _aiContentRepository;
    private readonly IStoryMemoryEngine _memoryEngine;
    private readonly IContentGuardrailService _guardrail;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IConfiguration _configuration;

    public AICoCreationService(
        IStoryRepository storyRepository,
        IChapterRepository chapterRepository,
        IAiGeneratedContentRepository aiContentRepository,
        IStoryMemoryEngine memoryEngine,
        IContentGuardrailService guardrail,
        IAIUsageLogRepository aiUsageLogRepository,
        IConfiguration configuration)
    {
        _storyRepository = storyRepository;
        _chapterRepository = chapterRepository;
        _aiContentRepository = aiContentRepository;
        _memoryEngine = memoryEngine;
        _guardrail = guardrail;
        _aiUsageLogRepository = aiUsageLogRepository;
        _configuration = configuration;
    }

    public async Task<CoCreationResponse> CoCreateAsync(
        CoCreationRequest request,
        Guid authorUserId,
        CancellationToken cancellationToken = default)
    {
        var story = _storyRepository.GetById(request.StoryId);
        if (story == null)
            throw new InvalidOperationException("Truyện không tồn tại.");
        if (story.author_id != authorUserId)
            throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được sử dụng tính năng đồng sáng tác.");

        string contextBlock = await _memoryEngine.BuildContextForCoCreateAsync(
            request.StoryId, request.AuthorIdea, cancellationToken);

        var storyLanguage = StoryLanguageHelper.DetectFromStoryContext(contextBlock);
        var languageInstruction = StoryLanguageHelper.GetLanguageInstruction(storyLanguage);

        // --- Agent 1 (Planner): Dàn ý hoặc phát hiện mâu thuẫn ý tưởng ---
        var (p1, m1, k1, u1) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlanner);
        var clientPlanner = AIClientHelper.CreateChatClient(p1, m1, k1, u1);
        var outlineJson = await RunAgent1OutlineAsync(clientPlanner, contextBlock, request.AuthorIdea, languageInstruction, cancellationToken);
        LogUsage(authorUserId, request.StoryId, null, ActionOutline, m1, 0, 0);

        var ideaFeedback = TryParseIdeaContradiction(outlineJson);
        if (ideaFeedback != null)
        {
            return new CoCreationResponse
            {
                IdeaContradictionFeedback = ideaFeedback,
                Outline = string.Empty,
                FinalContent = string.Empty,
                Approved = false,
                RevisionCount = 0,
                ReviewFeedback = null
            };
        }

        var outlineForPrompt = FormatOutlineForPrompt(outlineJson);

        // --- Agent 2 (Writer): Viết nội dung — model Mistral ---
        var (p2, m2, k2, u2) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentWriter);
        var clientWriter = AIClientHelper.CreateChatClient(p2, m2, k2, u2);
        string draft = await RunAgent2WriteAsync(clientWriter, contextBlock, outlineForPrompt, feedback: null, languageInstruction, cancellationToken);
        draft = StripTrailingFeedbackFromDraft(draft);
        LogUsage(authorUserId, request.StoryId, null, ActionWrite, m2, 0, 0);

        // --- Guardrail: từ cấm ---
        var guardrailResult = await _guardrail.CheckAsync(request.StoryId, draft, cancellationToken);
        var guardrailFeedback = guardrailResult.Passed
            ? null
            : string.Join(" ", guardrailResult.Violations.Select(v => $"[{v.Type}] {v.Message}"));

        int maxRevisions = _configuration.GetValue("AI:CoCreateMaxRevisions", DefaultMaxRevisions);
        if (maxRevisions < 0) maxRevisions = 0;

        // --- Agent 3 (Consistency Checker): Luôn chạy — model Llama 3 ---
        var (p3, m3, k3, u3) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);
        var clientChecker = AIClientHelper.CreateChatClient(p3, m3, k3, u3);
        var (approved, feedback) = await RunAgent3ReviewAsync(clientChecker, contextBlock, outlineForPrompt, draft, languageInstruction, cancellationToken);
        LogUsage(authorUserId, request.StoryId, null, ActionReview, m3, 0, 0);

        if (!guardrailResult.Passed)
        {
            approved = false;
            feedback = string.IsNullOrEmpty(feedback) ? guardrailFeedback : $"{guardrailFeedback}\n{feedback}";
        }

        int revisionCount = 0;
        string? lastFeedback = feedback;

        while (!approved && revisionCount < maxRevisions)
        {
            revisionCount++;
            draft = await RunAgent2WriteAsync(clientWriter, contextBlock, outlineForPrompt, lastFeedback, languageInstruction, cancellationToken);
            draft = StripTrailingFeedbackFromDraft(draft);
            LogUsage(authorUserId, request.StoryId, null, ActionWrite, m2, 0, 0);

            guardrailResult = await _guardrail.CheckAsync(request.StoryId, draft, cancellationToken);
            if (!guardrailResult.Passed)
                lastFeedback = string.Join(" ", guardrailResult.Violations.Select(v => $"[{v.Type}] {v.Message}"));
            else
            {
                var (approvedAgain, feedbackAgain) = await RunAgent3ReviewAsync(clientChecker, contextBlock, outlineForPrompt, draft, languageInstruction, cancellationToken);
                LogUsage(authorUserId, request.StoryId, null, ActionReview, m3, 0, 0);
                if (approvedAgain)
                {
                    var (chapterId, aiContentId) = SaveDraftChapterAndAiContent(request.StoryId, authorUserId, request.AuthorIdea, draft);
                    return new CoCreationResponse
                    {
                        Outline = outlineForPrompt,
                        FinalContent = draft,
                        Approved = true,
                        RevisionCount = revisionCount,
                        ReviewFeedback = null,
                        ChapterId = chapterId,
                        AiGeneratedContentId = aiContentId
                    };
                }
                lastFeedback = feedbackAgain;
            }
        }

        var saved = SaveDraftChapterAndAiContent(request.StoryId, authorUserId, request.AuthorIdea, draft);
        return new CoCreationResponse
        {
            Outline = outlineForPrompt,
            FinalContent = draft,
            Approved = approved,
            RevisionCount = revisionCount,
            ReviewFeedback = lastFeedback,
            ChapterId = saved.ChapterId,
            AiGeneratedContentId = saved.AiGeneratedContentId
        };
    }

    /// <summary>Một chapter DRAFT cho "chương tiếp theo"; mỗi lần co-create chỉ thêm một bản ai_generated_content cùng chapter_id để compare-chapter so với tất cả bản và lấy max similarity (dù tác giả chọn bản 1, 2 hay 3).</summary>
    private (Guid? ChapterId, Guid? AiGeneratedContentId) SaveDraftChapterAndAiContent(Guid storyId, Guid authorUserId, string authorIdea, string finalContent)
    {
        if (string.IsNullOrWhiteSpace(finalContent)) return (null, null);
        var chaptersList = _chapterRepository.GetByStoryId(storyId).ToList();
        var nextOrder = chaptersList.Count == 0 ? 1 : (chaptersList.Max(c => c.order_index) + 1);
        var now = DateTime.UtcNow;

        // Tìm chapter DRAFT đã có ít nhất một bản AI (slot "chương tiếp theo" đã được tạo bởi co-author trước đó)
        chapters? targetChapter = chaptersList
            .Where(c => string.Equals(c.status, "DRAFT", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(c => c.order_index)
            .FirstOrDefault(c => _aiContentRepository.GetAllByChapterId(c.id).Count > 0);

        if (targetChapter == null)
        {
            // Lần đầu co-author cho slot này: tạo chapter DRAFT mới
            var chapter = new chapters
            {
                id = Guid.NewGuid(),
                story_id = storyId,
                title = $"Bản nháp AI #{nextOrder}",
                order_index = nextOrder,
                content = finalContent,
                status = "DRAFT",
                word_count = finalContent.Length,
                created_at = now,
                updated_at = now
            };
            _chapterRepository.Add(chapter);
            var aiRecord = new ai_generated_content
            {
                id = Guid.NewGuid(),
                story_id = storyId,
                chapter_id = chapter.id,
                user_id = authorUserId,
                input_prompt = authorIdea.Length > 2000 ? authorIdea[..2000] + "..." : authorIdea,
                ai_output = finalContent,
                created_at = now
            };
            _aiContentRepository.Add(aiRecord);
            return (chapter.id, aiRecord.id);
        }

        // Đã có chapter DRAFT cho slot này: chỉ thêm bản ai_generated_content mới (cùng chapter_id)
        var aiRecordExisting = new ai_generated_content
        {
            id = Guid.NewGuid(),
            story_id = storyId,
            chapter_id = targetChapter.id,
            user_id = authorUserId,
            input_prompt = authorIdea.Length > 2000 ? authorIdea[..2000] + "..." : authorIdea,
            ai_output = finalContent,
            created_at = now
        };
        _aiContentRepository.Add(aiRecordExisting);
        // Cập nhật nội dung chapter = bản mới nhất để tác giả xem nhanh; khi chọn bản cũ thì UI có thể load từ ai_generated_content
        targetChapter.content = finalContent;
        targetChapter.updated_at = now;
        targetChapter.word_count = finalContent.Length;
        _chapterRepository.Update(targetChapter);
        return (targetChapter.id, aiRecordExisting.id);
    }

    private static string GetAgent1SystemPrompt() => """
Bạn là trợ lý viết dàn ý cho tác giả truyện. Dựa trên ngữ cảnh truyện (Story memory, Character/Event/Story State, RAG) và ý tưởng tác giả, viết dàn ý dạng các scene.

Bước 1 — Kiểm tra mâu thuẫn: Nếu ý tưởng tác giả MÂU THUẪN với ngữ cảnh (ví dụ: nhân vật đã chết/đã mất tích trong truyện nhưng ý tưởng lại nhắc nhân vật đó đang hành động, dạy dỗ, xuất hiện; hoặc sự kiện đã xảy ra nhưng ý tưởng mô tả ngược lại), thì KHÔNG tạo dàn ý. Trả về DUY NHẤT JSON:
{ "ideaContradiction": true, "feedback": "Mô tả ngắn cho tác giả (vd: Trong truyện sư phụ đã chết ở chương 2, không thể có cảnh sư phụ dạy chiêu mới.)" }
Ngôn ngữ feedback trùng truyện (Việt hoặc Anh).

Bước 2 — Nếu không mâu thuẫn: Dàn ý phải NỐI TIẾP mạch truyện, đúng logic và timeline; bám sát ý tưởng tác giả. Trả về DUY NHẤT JSON:
{ "scenes": [ { "title": "Tiêu đề scene", "summary": "Tóm tắt ngắn", "characters": ["Nhân vật 1", "Nhân vật 2"] } ] }
Ít nhất 1 scene; tối đa 10 scene. Ngôn ngữ trùng truyện (Việt hoặc Anh).
""" + "\n\n" + ConstitutionalRules;

    private async Task<string> RunAgent1OutlineAsync(ChatClient client, string contextBlock, string authorIdea, string languageInstruction, CancellationToken ct)
    {
        var userPrompt = $"Ngữ cảnh truyện:\n\n{contextBlock}\n\nÝ tưởng tác giả:\n{authorIdea}\n\n{languageInstruction}\n\nTrả về JSON dàn ý (scenes) theo đúng cấu trúc.";
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent1SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentPlanner);
        var completion = await client.CompleteChatAsync(messages, options);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent dàn ý không trả về nội dung.");
        return text.Trim();
    }

    /// <summary>Nếu Agent 1 trả về ideaContradiction (JSON thuần hoặc JSON nằm trong đoạn văn) thì trả về feedback; ngược lại null.</summary>
    private static string? TryParseIdeaContradiction(string outlineJson)
    {
        var raw = outlineJson.Trim();
        if (raw.StartsWith("```"))
        {
            var start = raw.IndexOf('\n') + 1;
            var end = raw.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start) raw = raw[start..end];
        }
        // Thử parse cả chuỗi như JSON
        try
        {
            var root = JsonDocument.Parse(raw).RootElement;
            if (root.TryGetProperty("ideaContradiction", out var ic) && ic.ValueKind == JsonValueKind.True)
            {
                return root.TryGetProperty("feedback", out var fb) ? fb.GetString()?.Trim() : "Ý tưởng tác giả mâu thuẫn với nội dung truyện đã có.";
            }
        }
        catch { /* ignore */ }
        // Trích nội dung trong ```json ... ``` nếu có (model hay trả về JSON trong markdown)
        var codeBlockStart = raw.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
        if (codeBlockStart >= 0)
        {
            var contentStart = raw.IndexOf('\n', codeBlockStart) + 1;
            var codeBlockEnd = raw.IndexOf("```", contentStart, StringComparison.Ordinal);
            if (contentStart > 0 && codeBlockEnd > contentStart)
            {
                var jsonBlock = raw[contentStart..codeBlockEnd].Trim();
                try
                {
                    var root = JsonDocument.Parse(jsonBlock).RootElement;
                    if (root.TryGetProperty("ideaContradiction", out var ic) && ic.ValueKind == JsonValueKind.True)
                    {
                        return root.TryGetProperty("feedback", out var fb) ? fb.GetString()?.Trim() : "Ý tưởng tác giả mâu thuẫn với nội dung truyện đã có.";
                    }
                }
                catch { /* ignore */ }
            }
        }
        // Đoạn văn có nhúng JSON (vd. "Trả về JSON:\n{ \"ideaContradiction\": true, ... }") → tìm { trước "ideaContradiction"
        var idx = raw.IndexOf("\"ideaContradiction\"", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = raw.IndexOf("ideaContradiction", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            // Dấu { mở object nằm trước "ideaContradiction" trong chuỗi
            var startObj = raw.LastIndexOf('{', idx);
            if (startObj >= 0)
            {
                int depth = 0, endObj = -1;
                for (int i = startObj; i < raw.Length; i++)
                {
                    if (raw[i] == '{') depth++;
                    else if (raw[i] == '}')
                    {
                        depth--;
                        if (depth == 0) { endObj = i; break; }
                    }
                }
                if (endObj > startObj)
                {
                    try
                    {
                        var jsonSpan = raw.Substring(startObj, endObj - startObj + 1);
                        var root = JsonDocument.Parse(jsonSpan).RootElement;
                        if (root.TryGetProperty("ideaContradiction", out var ic) && ic.ValueKind == JsonValueKind.True)
                        {
                            return root.TryGetProperty("feedback", out var fb) ? fb.GetString()?.Trim() : "Ý tưởng tác giả mâu thuẫn với nội dung truyện đã có.";
                        }
                    }
                    catch { /* ignore */ }
                }
            }
        }
        return null;
    }

    /// <summary>Parse outline JSON thành text để đưa vào Agent 2; nếu parse lỗi thì trả về raw.</summary>
    private static string FormatOutlineForPrompt(string outlineJson)
    {
        try
        {
            var raw = outlineJson.Trim();
            if (raw.StartsWith("```"))
            {
                var start = raw.IndexOf('\n') + 1;
                var end = raw.IndexOf("```", start, StringComparison.Ordinal);
                if (end > start) raw = raw[start..end];
            }
            var root = JsonDocument.Parse(raw).RootElement;
            if (!root.TryGetProperty("scenes", out var scenes) || scenes.GetArrayLength() == 0)
                return outlineJson;
            var lines = new List<string>();
            int i = 1;
            foreach (var scene in scenes.EnumerateArray())
            {
                var title = scene.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                var summary = scene.TryGetProperty("summary", out var s) ? s.GetString() ?? "" : "";
                var chars = scene.TryGetProperty("characters", out var c) ? c.EnumerateArray().Select(x => x.GetString()).Where(x => !string.IsNullOrEmpty(x)).ToArray() : Array.Empty<string>();
                lines.Add($"Scene {i}: {title}\n{summary}" + (chars.Length > 0 ? $"\nNhân vật: {string.Join(", ", chars)}" : ""));
                i++;
            }
            return string.Join("\n\n", lines);
        }
        catch
        {
            return outlineJson;
        }
    }

    private static string GetAgent2SystemPrompt() => """
Bạn là trợ lý viết nội dung truyện. Viết đoạn/chương nháp theo ĐÚNG dàn ý (các scene) được cung cấp.

Yêu cầu: Bám sát mạch truyện đang diễn ra và cốt truyện của các chương trước (timeline, nhân vật, quy tắc thế giới trong ngữ cảnh). Phong cách và giọng văn phù hợp truyện. Không viết ngược lại sự kiện đã xảy ra; nhân vật phải nhất quán với trạng thái đã nêu. Chỉ trả về nội dung văn bản, không tiêu đề hay giải thích. Ngôn ngữ trùng truyện.
""" + "\n\n" + ConstitutionalRules;

    private async Task<string> RunAgent2WriteAsync(ChatClient client, string contextBlock, string outline, string? feedback, string languageInstruction, CancellationToken ct)
    {
        var userPrompt = $"Ngữ cảnh truyện:\n\n{contextBlock}\n\nDàn ý cần viết:\n{outline}";
        if (!string.IsNullOrWhiteSpace(feedback))
            userPrompt += $"\n\nGóp ý cần sửa (bắt buộc tuân thủ):\n{feedback}";
        userPrompt += $"\n\n{languageInstruction}\n\nViết nội dung theo dàn ý (và góp ý nếu có).";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent2SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentWriter);
        var completion = await client.CompleteChatAsync(messages, options);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent viết nội dung không trả về nội dung.");
        return text.Trim();
    }

    /// <summary>Cắt bỏ đoạn "Feedback: ..." mà model đôi khi thêm vào cuối bản nháp; chỉ dùng reviewFeedback từ Agent 3.</summary>
    private static string StripTrailingFeedbackFromDraft(string draft)
    {
        if (string.IsNullOrWhiteSpace(draft)) return draft;
        var idx = draft.IndexOf("\n\nFeedback:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = draft.IndexOf("\nFeedback:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return draft[..idx].TrimEnd();
        return draft;
    }

    private static string GetAgent3SystemPrompt() => """
Bạn là Consistency Checker: kiểm duyệt nội dung truyện để đảm bảo bản nháp bám sát cốt truyện và không lệch logic. Đọc ngữ cảnh (Story memory, Character Memory, Event Memory, Story State, RAG), dàn ý và bản nháp.

Kiểm tra bắt buộc: (1) Mạch truyện — nội dung nối tiếp tự nhiên với các chương trước, không đứt mạch hoặc đổi hướng vô lý. (2) Timeline — sự kiện đúng thứ tự, không đảo ngược đã xảy ra. (3) Character — nhân vật đúng tính cách và trạng thái đã nêu (vd. đã chết thì không xuất hiện). (4) World rules — quy tắc thế giới truyện được tôn trọng. (5) Logic cốt truyện — không mâu thuẫn với sự kiện/chi tiết đã có. Bất kỳ mâu thuẫn hoặc lệch cốt truyện = chưa đạt.

Trả về DUY NHẤT một JSON hợp lệ, không markdown:
{ "approved": true } khi đạt.
{ "approved": false, "feedback": "Mô tả ngắn vấn đề để AI/tác giả sửa", "violations": [ { "type": "timeline|character|world_rules|logic|story_flow|contradiction|other", "quote": "đoạn trích (tùy chọn)" } ] } khi cần sửa.

Ngôn ngữ feedback: cùng ngôn ngữ truyện.
""" + "\n\n" + ConstitutionalRules;

    private async Task<(bool approved, string? feedback)> RunAgent3ReviewAsync(ChatClient client, string contextBlock, string outline, string draft, string languageInstruction, CancellationToken ct)
    {
        var userPrompt = $"Ngữ cảnh:\n\n{contextBlock}\n\nDàn ý:\n{outline}\n\nBản nháp:\n{draft}\n\n{languageInstruction}\n\nTrả về JSON theo đúng cấu trúc.";
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent3SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentConsistencyChecker);
        var completion = await client.CompleteChatAsync(messages, options);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            return (false, "Không đọc được kết quả kiểm duyệt.");
        return ParseReviewResult(text);
    }

    private static (bool approved, string? feedback) ParseReviewResult(string text)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var start = text.IndexOf('\n') + 1;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start) text = text[start..end];
        }
        try
        {
            var root = JsonDocument.Parse(text).RootElement;
            var approved = root.TryGetProperty("approved", out var a) && a.GetBoolean();
            var feedback = root.TryGetProperty("feedback", out var f) ? f.GetString() : null;
            if (!approved && string.IsNullOrWhiteSpace(feedback) && root.TryGetProperty("violations", out var v) && v.GetArrayLength() > 0)
            {
                var parts = new List<string>();
                foreach (var item in v.EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var quote = item.TryGetProperty("quote", out var q) ? q.GetString() : null;
                    parts.Add(string.IsNullOrEmpty(quote) ? $"[{type}]" : $"[{type}] {quote}");
                }
                feedback = string.Join(" ", parts);
            }
            return (approved, feedback);
        }
        catch
        {
            return (false, "Định dạng phản hồi kiểm duyệt không hợp lệ.");
        }
    }

    private void LogUsage(Guid userId, Guid storyId, Guid? chapterId, string actionType, string modelName, int promptTokens, int completionTokens)
    {
        _aiUsageLogRepository.Log(new ai_usage_logs
        {
            user_id = userId,
            story_id = storyId,
            chapter_id = chapterId,
            action_type = actionType,
            model_name = modelName,
            prompt_tokens = promptTokens,
            completion_tokens = completionTokens,
            total_tokens = promptTokens + completionTokens,
            status = "SUCCESS",
            created_at = DateTime.UtcNow
        });
    }
}
