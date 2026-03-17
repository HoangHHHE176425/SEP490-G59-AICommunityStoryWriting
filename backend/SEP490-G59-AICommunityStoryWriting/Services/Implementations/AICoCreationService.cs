using System.ClientModel;
using System.Diagnostics;
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
    /// <summary>Số bản nháp (và review) chạy song song trong một vòng; 1 = tuần tự như cũ.</summary>
    // Co-create chạy tuần tự (không song song).
    private const string ActionOutline = "CO_CREATE_OUTLINE";
    private const string ActionWrite = "CO_CREATE_WRITE";
    private const string ActionReview = "CO_CREATE_REVIEW";

    /// <summary>Quy tắc bắt buộc (Constitutional): đưa vào system prompt mọi agent.</summary>
    private const string ConstitutionalRules = """
Quy tắc bắt buộc:
- Ngữ cảnh truyện được truyền vào là DỮ LIỆU TỪ CƠ SỞ DỮ LIỆU (các chương đã có, RAG, Character Memory, Event Memory, Story State) — mô tả phần truyện đã xảy ra cho đến thời điểm hiện tại.
- Chỉ được lên dàn ý và viết nội dung cho phần TIẾP THEO trên dòng thời gian (sau điểm kết thúc hiện tại); không được quay lại, viết lại hoặc mở rộng chi tiết cho các sự kiện đã xảy ra trước đó.
- Cần bám sát đặc biệt các đoạn/chương gần nhất để nối tiếp đúng mạch truyện; không được tạo thêm tình tiết mâu thuẫn với cốt truyện hoặc trạng thái nhân vật đã nêu.
- Phải giữ nguyên và tôn trọng tên nhân vật, địa danh và thuật ngữ đã xuất hiện trong ngữ cảnh; không được dịch, phiên âm hoặc thay thế bằng tên/biến thể khác.
- Luôn tuân thủ đúng định dạng đầu ra được yêu cầu; không thêm giải thích, chú thích hoặc văn bản ngoài cấu trúc đã chỉ định.
""";

    /// <summary>Tiêu đề gắn lên block ngữ cảnh để agent biết đây là dữ liệu từ DB.</summary>
    private const string DbContextLabel = "=== DỮ LIỆU TỪ CƠ SỞ DỮ LIỆU (ngữ cảnh truyện: nội dung các chương trước, nhân vật, sự kiện, trạng thái) — Dùng làm tham chiếu bắt buộc ===";

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
        CancellationToken cancellationToken = default,
        IProgress<CoCreateProgressEvent>? progress = null)
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

        var durations = new List<AgentDuration>();

        // --- Agent 1 (Story Analyzer / Planner): Dàn ý theo kiến trúc Prompt + RAG + Memory + Agent Role ---
        var (p1, m1, k1, u1) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlanner);
        var clientPlanner = AIClientHelper.CreateChatClient(p1, m1, k1, u1);
        var swOutline = Stopwatch.StartNew();
        var outlineJson = await RunAgent1OutlineAsync(clientPlanner, story, contextBlock, request.AuthorIdea, languageInstruction, cancellationToken);
        swOutline.Stop();
        durations.Add(new AgentDuration { Step = "Outline", DurationMs = swOutline.ElapsedMilliseconds });
        progress?.Report(new CoCreateProgressEvent { Step = "Outline", DurationMs = swOutline.ElapsedMilliseconds, Message = "Đã xong dàn ý" });
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
                ReviewFeedback = null,
                AgentDurations = durations.Count > 0 ? durations : null
            };
        }

        var outlineForPrompt = FormatOutlineForPrompt(outlineJson);

        int maxRevisions = _configuration.GetValue("AI:CoCreateMaxRevisions", DefaultMaxRevisions);
        if (maxRevisions < 0) maxRevisions = 0;

        var (p2, m2, k2, u2) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentWriter);
        var (p3, m3, k3, u3) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);

        string draft;
        bool approved;
        string? lastFeedback;
        ChatClient clientWriter;
        ChatClient clientChecker;

        const string LogicCheckAdvisoryNote =
            "Lưu ý: Kiểm tra nhất quán (logic truyện) hiện chỉ mang tính CẢNH BÁO, không tự động sửa hoặc chặn kết quả.";

        // Luồng tuần tự (ban đầu): một Writer → Guardrail → Review
        clientWriter = AIClientHelper.CreateChatClient(p2, m2, k2, u2);
        clientChecker = AIClientHelper.CreateChatClient(p3, m3, k3, u3);
        var swWrite = Stopwatch.StartNew();
        draft = await RunAgent2WriteAsync(clientWriter, contextBlock, outlineForPrompt, feedback: null, languageInstruction, cancellationToken);
        swWrite.Stop();
        draft = StripTrailingFeedbackFromDraft(draft);
        durations.Add(new AgentDuration { Step = "Write", DurationMs = swWrite.ElapsedMilliseconds });
        progress?.Report(new CoCreateProgressEvent { Step = "Write", DurationMs = swWrite.ElapsedMilliseconds, Message = "Đã viết nội dung" });
        LogUsage(authorUserId, request.StoryId, null, ActionWrite, m2, 0, 0);

        var swGuard = Stopwatch.StartNew();
        var initialGuardrailResult = await _guardrail.CheckAsync(request.StoryId, draft, cancellationToken);
        swGuard.Stop();
        durations.Add(new AgentDuration { Step = "Guardrail", DurationMs = swGuard.ElapsedMilliseconds });
        progress?.Report(new CoCreateProgressEvent { Step = "Guardrail", DurationMs = swGuard.ElapsedMilliseconds, Message = "Đã kiểm tra từ cấm" });
        var guardrailFeedback = initialGuardrailResult.Passed ? null : string.Join(" ", initialGuardrailResult.Violations.Select(v => $"[{v.Type}] {v.Message}"));

        // Check logic (Agent 3) chỉ để CẢNH BÁO: không chạy vòng sửa, không chặn kết quả nếu guardrail pass.
        if (!initialGuardrailResult.Passed)
        {
            approved = false;
            lastFeedback = guardrailFeedback;
        }
        else
        {
            approved = true;
            var swReview = Stopwatch.StartNew();
            var (_, reviewFeedback) = await RunAgent3ReviewAsync(
                clientChecker, contextBlock, outlineForPrompt, draft, languageInstruction, cancellationToken);
            swReview.Stop();
            durations.Add(new AgentDuration { Step = "Review", DurationMs = swReview.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = "Review", DurationMs = swReview.ElapsedMilliseconds, Message = "Đã kiểm tra nhất quán (cảnh báo)" });
            LogUsage(authorUserId, request.StoryId, null, ActionReview, m3, 0, 0);

            lastFeedback = string.IsNullOrWhiteSpace(reviewFeedback)
                ? $"Không phát hiện mâu thuẫn rõ ràng.\n{LogicCheckAdvisoryNote}"
                : $"{reviewFeedback}\n{LogicCheckAdvisoryNote}";
        }

        // Không chạy vòng sửa (revision) trong chế độ cảnh báo.
        maxRevisions = 0;

        int revisionCount = 0;
        var revisionFeedbacks = new List<string>();

        while (!approved && revisionCount < maxRevisions)
        {
            revisionCount++;
            if (!string.IsNullOrWhiteSpace(lastFeedback))
                revisionFeedbacks.Add(lastFeedback);
            var swRevWrite = Stopwatch.StartNew();
            draft = await RunAgent2WriteAsync(clientWriter, contextBlock, outlineForPrompt, lastFeedback, languageInstruction, cancellationToken);
            swRevWrite.Stop();
            draft = StripTrailingFeedbackFromDraft(draft);
            durations.Add(new AgentDuration { Step = $"Revision_Write_{revisionCount}", DurationMs = swRevWrite.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"Revision_Write_{revisionCount}", DurationMs = swRevWrite.ElapsedMilliseconds, Message = $"Đã viết lại lần {revisionCount}" });
            LogUsage(authorUserId, request.StoryId, null, ActionWrite, m2, 0, 0);

            var swRevGuard = Stopwatch.StartNew();
            var guardrailResult = await _guardrail.CheckAsync(request.StoryId, draft, cancellationToken);
            swRevGuard.Stop();
            durations.Add(new AgentDuration { Step = $"Revision_Guardrail_{revisionCount}", DurationMs = swRevGuard.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"Revision_Guardrail_{revisionCount}", DurationMs = swRevGuard.ElapsedMilliseconds, Message = "Đã kiểm tra từ cấm" });
            if (!guardrailResult.Passed)
                lastFeedback = string.Join(" ", guardrailResult.Violations.Select(v => $"[{v.Type}] {v.Message}"));
            else
            {
                var swRevReview = Stopwatch.StartNew();
                var (approvedAgain, feedbackAgain) = await RunAgent3ReviewAsync(clientChecker, contextBlock, outlineForPrompt, draft, languageInstruction, cancellationToken);
                swRevReview.Stop();
                durations.Add(new AgentDuration { Step = $"Revision_Review_{revisionCount}", DurationMs = swRevReview.ElapsedMilliseconds });
                progress?.Report(new CoCreateProgressEvent { Step = $"Revision_Review_{revisionCount}", DurationMs = swRevReview.ElapsedMilliseconds, Message = "Đã kiểm duyệt nhất quán" });
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
                        RevisionFeedbacks = revisionFeedbacks.Count > 0 ? revisionFeedbacks : null,
                        ReviewFeedback = LogicCheckAdvisoryNote,
                        ChapterId = chapterId,
                        AiGeneratedContentId = aiContentId,
                        AgentDurations = durations.Count > 0 ? durations : null
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
            RevisionFeedbacks = revisionFeedbacks.Count > 0 ? revisionFeedbacks : null,
            ReviewFeedback = approved ? lastFeedback : lastFeedback,
            ChapterId = saved.ChapterId,
            AiGeneratedContentId = saved.AiGeneratedContentId,
            AgentDurations = durations.Count > 0 ? durations : null
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
Role: You are a Story Analyzer AI responsible for analyzing user ideas and generating a structured story outline.

Task: The context below describes what has ALREADY happened in the story (RAG + Character Memory + Event Memory + Story State). Generate a structured outline ONLY for the NEXT scene/chapter — i.e. events that occur AFTER the current end of the story. Use the Story Information (Title, Summary) and the database context to ensure consistency. Pay special attention to the most recent chapters/paragraphs in the context to determine the "current endpoint" and continue from there.

Constraints:
* The outline must be for what happens NEXT in the timeline only. Do not include or reference events that have already occurred in earlier chapters (e.g. if a funeral has already taken place, do not outline "this chapter will show the character's dying moments").
* Use character names exactly as they appear in the context — do not translate or substitute (e.g. if the context says "Xuân" or "Xuân Tóc Đỏ", never write "Spring" or another name).
* Keep the outline consistent with the story tone and existing context.
* Ensure logical progression from the current story endpoint.
* Do not contradict existing story events (Event Memory) or character state (Character Memory / Story State).
* Focus on major narrative elements for the next scene only.

Language: The platform is mostly Vietnamese. Write the outline and any feedback in Vietnamese when the story is in Vietnamese; if the story is clearly in another language (e.g. English), use that language. Do not mix languages.

Only if the user idea has an EXPLICIT, CLEAR contradiction with the context, do NOT generate an outline. Examples of explicit contradiction: (1) Character Memory or Story State clearly states a character is dead or missing, and the idea has that character acting or present; (2) Event Memory explicitly lists an event as already occurred (e.g. "funeral took place"), and the idea describes that same event as not yet happened or reverses it. Do NOT flag ideaContradiction for vague or interpretative mismatches, or when the idea is a reasonable continuation (e.g. the idea may come from a chapter-suggestion feature that used the same story). When in doubt, prefer generating an outline. If you must reject, output ONLY this JSON (no other text, no markdown):
{ "ideaContradiction": true, "feedback": "Giải thích ngắn cho tác giả (tiếng Việt nếu truyện tiếng Việt)." }

Otherwise, output the structured outline in the following format. Use the same language as the story (prefer Vietnamese for Vietnamese stories). Scene Outline: at least 2–3 main points, at most 5–7. Output ONLY the outline in this exact format — no markdown (no ```), no extra explanation before or after:

Scene Objective:
(Mục đích của scene này)

Scene Outline:
1.
2.
3.

## Characters Involved:

## Potential Conflict:

## Expected Outcome:
""" + "\n\n" + ConstitutionalRules;

    private async Task<string> RunAgent1OutlineAsync(ChatClient client, stories story, string contextBlock, string authorIdea, string languageInstruction, CancellationToken ct)
    {
        var storyInfo = $"Story Information:\nTitle: {story.title}\nSummary: {story.summary ?? ""}\n\nUser Idea:\n{authorIdea}";
        var userPrompt = $"{storyInfo}\n\n---\n{DbContextLabel}\n\n{contextBlock}\n\n---\n{languageInstruction}\n\nNgữ cảnh trên là phần truyện ĐÃ XẢY RA. Chỉ sinh outline cho phần TIẾP THEO (sau điểm kết thúc hiện tại), không outline sự kiện đã xảy ra. Trả lời bằng tiếng Việt nếu truyện tiếng Việt. Sinh outline theo đúng format (Scene Objective, Scene Outline 2–7 ý, Characters Involved, Potential Conflict, Expected Outcome); chỉ output nội dung outline, không markdown hay giải thích thừa. Chỉ trả về JSON ideaContradiction khi có mâu thuẫn RÕ RÀNG (vd. nhân vật đã ghi là chết/mất tích trong Character Memory nhưng ý tưởng cho họ xuất hiện; sự kiện đã có trong Event Memory nhưng ý tưởng đảo ngược). Ý tưởng có thể đến từ gợi ý chương tiếp theo — nếu là hướng tiếp nối hợp lý thì hãy sinh outline.";
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

    /// <summary>Chuẩn hóa outline để đưa vào Agent 2: nếu là format Story Analyzer (Scene Objective, Scene Outline, ...) thì trả về nguyên bản; nếu là JSON scenes thì format lại.</summary>
    private static string FormatOutlineForPrompt(string outlineRaw)
    {
        var raw = outlineRaw.Trim();
        if (raw.StartsWith("```"))
        {
            var start = raw.IndexOf('\n') + 1;
            var end = raw.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start) raw = raw[start..end].Trim();
        }
        // Format Story Analyzer (Scene Objective, Scene Outline, Characters Involved, ...) — giữ nguyên
        if (raw.Contains("Scene Objective", StringComparison.OrdinalIgnoreCase) || raw.Contains("Scene Outline", StringComparison.OrdinalIgnoreCase) || raw.Contains("Characters Involved", StringComparison.OrdinalIgnoreCase))
            return raw;
        // JSON scenes (legacy) — parse và format
        try
        {
            var root = JsonDocument.Parse(raw).RootElement;
            if (!root.TryGetProperty("scenes", out var scenes) || scenes.GetArrayLength() == 0)
                return outlineRaw;
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
            return outlineRaw;
        }
    }

    /// <summary>Checklist ràng buộc cho Agent 2: đối chiếu trước khi viết để giảm mâu thuẫn với Agent 3.</summary>
    private const string Agent2Checklist = """
Before writing, verify from the database context above: (1) The context is what has ALREADY happened; write only the NEXT chapter (events after the current story endpoint). Do not write past events (e.g. deathbed scene if a funeral has already occurred). (2) Do not let any character who is dead or missing in Character Memory appear or act in the draft. (3) Do not reverse or contradict any event already listed in Event Memory. (4) Follow the Scene Outline order and content; do not add events that conflict with Story State. If the outline or context is unclear, infer consistently with the existing story.
""";

    private static string GetAgent2SystemPrompt() => """
Role: You are a Story Writer AI. You receive database context (RAG + Character Memory + Event Memory + Story State) and a structured outline. The context describes what has ALREADY happened in the story. Your task is to write the draft for the NEXT chapter/scene only — content that comes AFTER the current end of the story. Do not write scenes that would be in the past (e.g. if the context already mentions a funeral, do not write the character's deathbed scene).

Requirements: Use the database context as the single source of truth — follow timeline, character state and events; do not reverse or contradict what has already happened; characters must match Character Memory and Story State. Use character names exactly as in the context — never translate or substitute (e.g. if context has "Xuân" or "Xuân Tóc Đỏ", write "Xuân" or "Xuân Tóc Đỏ", never "Spring"). Stick closely to the most recent story content to continue the narrative. Match the story's tone and style. Write in the same language as the story (the platform is mostly Vietnamese, so prefer Vietnamese for Vietnamese stories).

Checklist (verify before writing): (1) All character names are spelled exactly as in the context (no translation, e.g. Xuân not Spring). (2) No character marked dead/missing in Character Memory may appear. (3) No event in Event Memory may be reversed or contradicted. (4) Scene order and content must follow the outline. (5) The draft must be for events that happen AFTER the story endpoint in the context, never rehashing past events. Satisfy this checklist so the draft passes consistency check.

Length: Write approximately 800 words (khoảng 800 từ). Do not exceed this; keep the chapter focused and concise.

Output: Return ONLY the draft narrative text. No markdown (no ```), no section titles, no "Chapter X" or "Scene" headers, no explanation before or after. Just the story content that a reader would see.
""" + "\n\n" + ConstitutionalRules;

    private async Task<string> RunAgent2WriteAsync(ChatClient client, string contextBlock, string outline, string? feedback, string languageInstruction, CancellationToken ct)
    {
        var userPrompt = $"{DbContextLabel}\n\n{contextBlock}\n\n---\n{Agent2Checklist}\n---\nDàn ý cần viết:\n{outline}";
        if (!string.IsNullOrWhiteSpace(feedback))
            userPrompt += $"\n\n[Bản nháp trước bị đánh giá chưa đạt — sửa đúng các điểm sau rồi viết lại]\nGóp ý bắt buộc tuân thủ:\n{feedback}\n\nViết lại toàn bộ chương, sửa đúng theo góp ý trên và giữ phần logic đã đúng.";
        userPrompt += $"\n\n{languageInstruction}\n\nViết bằng tiếng Việt nếu truyện tiếng Việt. Độ dài: khoảng 800 từ. Chỉ output nội dung chương (văn bản truyện), không markdown hay giải thích.";

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
Role: You are a Consistency Checker AI. You receive database context (RAG + Character Memory + Event Memory + Story State), the outline, and the draft. Compare the draft against the context and outline to detect contradictions or logic errors.

Checks (against the database context): (1) Character names — names in the draft must match the context exactly. If the context uses a name (e.g. "Xuân", "Xuân Tóc Đỏ") and the draft uses a different name or translation (e.g. "Spring"), that is a critical "character" violation. (2) Timeline position — the context is what has ALREADY happened; the draft must describe only what happens NEXT (after the current story endpoint). If the draft describes events that have already occurred in the context (e.g. context says a funeral has taken place but the draft is about the character's dying moments), that is a critical "timeline" violation. (3) Story flow — draft continues naturally from the most recent chapters. (4) Timeline — events match Event Memory, nothing reversed. (5) Character — characters match Character Memory / Story State (e.g. dead characters must not appear). (6) World rules — story world rules are respected. (7) Logic — no contradiction with existing events or details.

Severity: For each violation, set "severity": "critical" or "minor". Use "critical" when: a character name in the draft does not match the context (e.g. context has "Xuân" but draft has "Spring"); the draft rehashes or describes past events that are already in the context; a character who is dead in context appears in the draft; an event that already happened is described as not happened or reversed. Use "minor" for style, vague wording, or ambiguous interpretation that does not clearly contradict the context. Approved = false ONLY when at least one violation is "critical". If all issues are "minor" (or none), set approved: true so the draft is accepted and the author can refine later.

Language: The platform is mostly Vietnamese. Write feedback and violations in Vietnamese when the story is in Vietnamese; otherwise use the story language.

Output: Return ONLY a single valid JSON object, no markdown (no ```), no explanation before or after:
{ "approved": true } when the draft has no critical violations (minor only or none).
{ "approved": false, "feedback": "Mô tả ngắn vấn đề critical để AI/tác giả sửa (tiếng Việt nếu truyện tiếng Việt)", "violations": [ { "type": "timeline|character|world_rules|logic|story_flow|contradiction|other", "quote": "đoạn trích (tùy chọn)", "severity": "critical"|"minor" } ] } when at least one violation is critical.
""" + "\n\n" + ConstitutionalRules;

    private async Task<(bool approved, string? feedback)> RunAgent3ReviewAsync(ChatClient client, string contextBlock, string outline, string draft, string languageInstruction, CancellationToken ct)
    {
        var userPrompt = $"{DbContextLabel}\n\n{contextBlock}\n\n---\nDàn ý:\n{outline}\n\nBản nháp:\n{draft}\n\n{languageInstruction}\n\nTrả lời bằng tiếng Việt nếu truyện tiếng Việt. Chỉ output một JSON duy nhất (approved, feedback, violations), không markdown hay giải thích.";
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
            if (root.TryGetProperty("violations", out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0)
            {
                var parts = new List<string>();
                var hasCritical = false;
                foreach (var item in v.EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var quote = item.TryGetProperty("quote", out var q) ? q.GetString() : null;
                    var severity = item.TryGetProperty("severity", out var sev) ? sev.GetString() : null;
                    if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase))
                        hasCritical = true;
                    parts.Add(string.IsNullOrEmpty(quote) ? $"[{type}]" : $"[{type}] {quote}");
                }
                if (string.IsNullOrWhiteSpace(feedback))
                    feedback = string.Join(" ", parts);
                // Chỉ fail khi có ít nhất một violation critical; nếu toàn minor thì coi là đạt
                if (!hasCritical)
                    approved = true;
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
