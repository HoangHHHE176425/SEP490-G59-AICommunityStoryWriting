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

1. Nguồn dữ liệu:
- Ngữ cảnh truyện được truyền vào là DỮ LIỆU DUY NHẤT (single source of truth), bao gồm: Story Information, RAG, Character Memory, Event Memory, Story State.
- Mọi nội dung sinh ra PHẢI tuân thủ dữ liệu này, không được suy diễn hoặc tự ý thay đổi.

2. Thứ tự ưu tiên ngữ cảnh (bắt buộc tuân theo):
- Story State (cao nhất)
- Event Memory
- Character Memory
- RAG / nội dung trước đó (tham khảo)

Nếu có mâu thuẫn, phải tuân theo thứ tự này.

3. Timeline:
- Chỉ được viết nội dung xảy ra SAU điểm kết thúc hiện tại của truyện.
- Điểm kết thúc hiện tại được xác định là sự kiện CUỐI CÙNG trong đoạn/chương gần nhất của ngữ cảnh.
- Nghiêm cấm:
  + Viết lại sự kiện đã xảy ra
  + Mô tả lại quá khứ như nội dung chính của scene mới

4. Tính nhất quán:
- Không được tạo ra bất kỳ chi tiết nào mâu thuẫn với:
  + Event Memory (timeline sự kiện)
  + Character Memory (trạng thái nhân vật)
  + Story State (trạng thái hiện tại)
- Không được đảo ngược hoặc phủ định sự kiện đã xảy ra

5. Nhân vật và tên riêng:
- Phải sử dụng chính xác tên nhân vật, địa danh, thuật ngữ như trong ngữ cảnh
- Không dịch, không thay thế, không biến đổi tên dưới bất kỳ hình thức nào

6. Kiểm soát nội dung mới:
- Không tự ý thêm:
  + Nhân vật quan trọng mới
  + Sức mạnh mới
  + Phe phái mới
  + Plot twist lớn
→ trừ khi đã được chuẩn bị hoặc gợi ý rõ trong ngữ cảnh

7. Liên kết mạch truyện:
- Phải bám sát các đoạn/chương gần nhất để đảm bảo tiếp nối tự nhiên
- Không được nhảy cảnh hoặc thay đổi hướng truyện đột ngột

8. Không mở rộng quá khứ:
- Không được lấy sự kiện đã xảy ra và viết lại chi tiết hơn như một nội dung mới
- Chỉ được tham chiếu ngắn gọn nếu cần thiết cho ngữ cảnh

9. Định dạng đầu ra:
- Phải tuân thủ chính xác format được yêu cầu
- Không thêm giải thích, ghi chú hoặc nội dung ngoài yêu cầu
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

        var rawIdea = request.AuthorIdea?.Trim();
        var hasAuthorIdea = !string.IsNullOrWhiteSpace(rawIdea);
        var effectiveIdea = hasAuthorIdea
            ? rawIdea!
            : "Hãy viết tiếp chương tiếp theo một cách tự nhiên dựa trên mạch truyện hiện có (không thêm plot twist lớn nếu chưa được gợi mở).";

        string contextBlock = await _memoryEngine.BuildContextForCoCreateAsync(
            request.StoryId, effectiveIdea, cancellationToken);

        var storyLanguage = StoryLanguageHelper.DetectFromStoryContext(contextBlock);
        var languageInstruction = StoryLanguageHelper.GetLanguageInstruction(storyLanguage);

        var durations = new List<AgentDuration>();

        // --- Agent 1 (Story Analyzer / Planner): Dàn ý theo kiến trúc Prompt + RAG + Memory + Agent Role ---
        var (p1, m1, k1, u1) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlanner);
        var clientPlanner = AIClientHelper.CreateChatClient(p1, m1, k1, u1);
        var swOutline = Stopwatch.StartNew();
        var outlineJson = await RunAgent1OutlineAsync(clientPlanner, story, contextBlock, effectiveIdea, languageInstruction, cancellationToken);
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
        string? reviewFeedback;
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

        // --- Agent 3 (Validator): targeted fix loop ---
        // Guardrail (từ cấm) vẫn là bước bắt buộc. Validator kiểm tra logic/nhất quán và trả về violations có thể sửa.

        ReviewResult review;
        if (!initialGuardrailResult.Passed)
        {
            approved = false;
            reviewFeedback = guardrailFeedback;
            review = new ReviewResult(false, guardrailFeedback, new List<ReviewViolation>());
        }
        else
        {
            var swReview = Stopwatch.StartNew();
            review = await RunAgent3ReviewAsync(clientChecker, contextBlock, outlineForPrompt, draft, effectiveIdea, languageInstruction, cancellationToken);
            swReview.Stop();
            durations.Add(new AgentDuration { Step = "Review", DurationMs = swReview.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = "Review", DurationMs = swReview.ElapsedMilliseconds, Message = "Đã kiểm duyệt nhất quán" });
            LogUsage(authorUserId, request.StoryId, null, ActionReview, m3, 0, 0);
            approved = review.Approved;
            reviewFeedback = review.Feedback;
        }

        int revisionCount = 0;
        var revisionFeedbacks = new List<string>();

        while (!approved && revisionCount < maxRevisions)
        {
            revisionCount++;
            if (!string.IsNullOrWhiteSpace(reviewFeedback))
                revisionFeedbacks.Add(reviewFeedback);

            // Extract violations for targeted fix. If none, fallback to using feedback text only.
            var violations = review.Violations.Where(v => v.Severity.Equals("critical", StringComparison.OrdinalIgnoreCase)).ToList();
            var swFix = Stopwatch.StartNew();
            draft = await RunAgent2FixAsync(
                clientWriter,
                contextBlock,
                outlineForPrompt,
                draft,
                violations,
                reviewFeedback,
                languageInstruction,
                cancellationToken);
            swFix.Stop();
            draft = StripTrailingFeedbackFromDraft(draft);
            durations.Add(new AgentDuration { Step = $"Revision_Fix_{revisionCount}", DurationMs = swFix.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"Revision_Fix_{revisionCount}", DurationMs = swFix.ElapsedMilliseconds, Message = $"Đã sửa lỗi lần {revisionCount}" });
            LogUsage(authorUserId, request.StoryId, null, ActionWrite, m2, 0, 0);

            var swRevGuard = Stopwatch.StartNew();
            var guardrailResult = await _guardrail.CheckAsync(request.StoryId, draft, cancellationToken);
            swRevGuard.Stop();
            durations.Add(new AgentDuration { Step = $"Revision_Guardrail_{revisionCount}", DurationMs = swRevGuard.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"Revision_Guardrail_{revisionCount}", DurationMs = swRevGuard.ElapsedMilliseconds, Message = "Đã kiểm tra từ cấm" });
            if (!guardrailResult.Passed)
            {
                approved = false;
                reviewFeedback = string.Join(" ", guardrailResult.Violations.Select(v => $"[{v.Type}] {v.Message}"));
                review = new ReviewResult(false, reviewFeedback, new List<ReviewViolation>());
                continue;
            }

            var swRevReview = Stopwatch.StartNew();
            review = await RunAgent3ReviewAsync(clientChecker, contextBlock, outlineForPrompt, draft, effectiveIdea, languageInstruction, cancellationToken);
            swRevReview.Stop();
            durations.Add(new AgentDuration { Step = $"Revision_Review_{revisionCount}", DurationMs = swRevReview.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"Revision_Review_{revisionCount}", DurationMs = swRevReview.ElapsedMilliseconds, Message = "Đã kiểm duyệt nhất quán" });
            LogUsage(authorUserId, request.StoryId, null, ActionReview, m3, 0, 0);

            approved = review.Approved;
            reviewFeedback = review.Feedback;
        }

        var saved = SaveAiGeneratedContentOnly(
            request.StoryId,
            authorUserId,
            hasAuthorIdea ? rawIdea! : "[AUTO] Tiếp tục theo mạch truyện (không có gợi ý tác giả)",
            draft);
        return new CoCreationResponse
        {
            Outline = outlineForPrompt,
            FinalContent = draft,
            Approved = approved,
            RevisionCount = revisionCount,
            RevisionFeedbacks = revisionFeedbacks.Count > 0 ? revisionFeedbacks : null,
            ReviewFeedback = approved ? null : reviewFeedback,
            ChapterId = null,
            AiGeneratedContentId = saved.Id,
            ChapterIndex = saved.ChapterIndex,
            AgentDurations = durations.Count > 0 ? durations : null
        };
    }

    private sealed record ReviewViolation(string Type, string Severity, string? Quote, string? Fix);
    private sealed record ReviewResult(bool Approved, string? Feedback, List<ReviewViolation> Violations);

    /// <summary>Chỉ lưu bản <see cref="ai_generated_content"/> (không tạo/cập nhật <see cref="chapters"/>). <see cref="ai_generated_content.chapter_index"/> = slot chương tiếp theo (như <c>order_index</c> khi tạo chương).</summary>
    private (Guid? Id, int? ChapterIndex) SaveAiGeneratedContentOnly(Guid storyId, Guid authorUserId, string authorIdea, string finalContent)
    {
        if (string.IsNullOrWhiteSpace(finalContent)) return (null, null);
        var chaptersList = _chapterRepository.GetByStoryId(storyId).ToList();
        var nextChapterIndex = chaptersList.Count == 0 ? 1 : chaptersList.Max(c => c.order_index) + 1;
        var now = DateTime.UtcNow;
        var aiRecord = new ai_generated_content
        {
            id = Guid.NewGuid(),
            story_id = storyId,
            chapter_id = null,
            chapter_index = nextChapterIndex,
            user_id = authorUserId,
            input_prompt = authorIdea.Length > 2000 ? authorIdea[..2000] + "..." : authorIdea,
            ai_output = finalContent,
            created_at = now
        };
        _aiContentRepository.Add(aiRecord);
        return (aiRecord.id, nextChapterIndex);
    }

    private static string GetAgent1SystemPrompt() => """
Role:
Bạn là AI phân tích truyện (Story Analyzer), chịu trách nhiệm phân tích ý tưởng của người dùng và tạo dàn ý có cấu trúc cho phần TIẾP THEO của câu chuyện.

Task:
Ngữ cảnh dưới đây mô tả những gì ĐÃ xảy ra trong truyện (bao gồm: Story Information, RAG, Character Memory, Event Memory, Story State).
Nhiệm vụ của bạn là tạo dàn ý CHỈ cho scene/chapter TIẾP THEO — tức là các sự kiện xảy ra SAU điểm kết thúc hiện tại của truyện.

Bạn phải xác định chính xác “điểm kết thúc hiện tại” dựa trên:
→ Sự kiện CUỐI CÙNG trong đoạn gần nhất của context (ưu tiên đoạn mới nhất / cuối cùng).

Tất cả các diễn biến trong outline phải xảy ra SAU điểm này.

---

Context Priority (thứ tự ưu tiên, từ cao xuống thấp):
1. Story State (trạng thái hiện tại của truyện)
2. Event Memory (timeline sự kiện)
3. Character Memory (trạng thái nhân vật)
4. RAG / Previous Content (chỉ để tham khảo)

Nếu có mâu thuẫn, phải tuân theo thứ tự ưu tiên này.

---

Constraints:
- CHỈ viết những gì xảy ra TIẾP THEO, không lặp lại hoặc mô tả lại sự kiện đã xảy ra.
- Không phá vỡ logic truyện hoặc timeline đã có.
- Không thay đổi tính cách nhân vật nếu không có lý do hợp lý từ context.
- Không tự ý thêm yếu tố lớn (sức mạnh mới, phe phái mới, plot twist lớn) nếu chưa được chuẩn bị trước trong context.
- Sử dụng chính xác tên nhân vật như trong context (không dịch, không thay đổi).
- Đảm bảo diễn tiến hợp lý từ điểm kết thúc hiện tại.
- Giữ đúng tone của truyện (dark, romance, comedy,...).

---

Xử lý mâu thuẫn ý tưởng:
CHỈ khi ý tưởng của người dùng có mâu thuẫn RÕ RÀNG với context thì mới từ chối.

Ví dụ mâu thuẫn rõ:
- Nhân vật đã chết nhưng lại xuất hiện bình thường
- Một sự kiện đã xảy ra nhưng bị đảo ngược hoàn toàn

Nếu mâu thuẫn nhẹ hoặc có thể điều chỉnh:
→ TỰ ĐIỀU CHỈNH ý tưởng để phù hợp với context, KHÔNG từ chối.

Nếu ý tưởng của tác giả CỐ Ý tạo plot twist/retcon (ví dụ: muốn nhân vật đã hy sinh “trở lại”):
→ KHÔNG từ chối ngay.
→ Outline bắt buộc phải thêm một hoặc nhiều bước “giải thích hợp lý” để làm plot twist THUYẾT PHỤC và nhất quán (ví dụ: hiểu nhầm/giả chết, hồi ức, song sinh, cứu kịp thời, phép thuật đã được gợi mở, v.v.).
→ Nếu thế giới truyện không cho phép (không có cơ chế hợp lý nào trong context) thì đề xuất phương án plot twist “ít phá” hơn và vẫn bám ý tưởng tác giả.

Nếu bắt buộc phải từ chối, chỉ trả về JSON:
{ "ideaContradiction": true, "feedback": "Giải thích ngắn gọn bằng tiếng Việt." }

---

Yêu cầu về Scene Outline:
- Gồm 2–7 ý chính (bullet points)
- Mỗi ý PHẢI bao gồm:
  + Hành động chính (điều gì xảy ra)
  + Nhân vật liên quan
  + Mục đích / ý nghĩa (tại sao quan trọng)

- Ý cuối cùng PHẢI tạo:
  + xung đột mới / cao trào / hoặc câu hỏi mở (hook) để dẫn sang scene tiếp theo

---

Ngôn ngữ:
- Nếu truyện là tiếng Việt → viết hoàn toàn bằng tiếng Việt
- Nếu truyện là tiếng Anh → viết bằng tiếng Anh
- Không trộn ngôn ngữ

---

Output Format (bắt buộc, không thêm gì khác):

Scene Objective:
(Mục đích của scene này)

Scene Outline:
1.
2.
3.

Characters Involved:

Potential Conflict:

Expected Outcome:
(Kết quả dự kiến của scene, 1–3 câu)

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
Trước khi viết, hãy tự kiểm tra dựa trên “Ngữ cảnh từ database” phía trên:
(1) Ngữ cảnh mô tả những gì ĐÃ xảy ra; bạn chỉ được viết phần TIẾP THEO (các sự kiện xảy ra SAU điểm kết thúc hiện tại). Tuyệt đối không viết lại quá khứ (ví dụ: ngữ cảnh đã nhắc tang lễ thì không được viết cảnh hấp hối/qua đời như đang diễn ra).
(2) Không để nhân vật đã chết/mất tích (trong Character Memory / Story State) xuất hiện hoặc hành động trong bản nháp.
(3) Không đảo ngược, phủ định, hoặc mâu thuẫn với các sự kiện đã có trong Event Memory.
(4) Bám đúng thứ tự và nội dung của Scene Outline; không thêm sự kiện gây mâu thuẫn với Story State.
Nếu outline hoặc context còn mơ hồ, hãy suy luận theo hướng PHÙ HỢP NHẤT với truyện đã có, ưu tiên tính nhất quán và logic.
""";

    private static string GetAgent2SystemPrompt() => """
Role:
Bạn là AI viết truyện (Story Writer), chịu trách nhiệm viết bản nháp cho scene/chapter TIẾP THEO dựa trên outline và ngữ cảnh đã có.

---

Task:
Bạn nhận được:
- Ngữ cảnh từ database (Story Information, RAG, Character Memory, Event Memory, Story State)
- Một dàn ý có cấu trúc (Scene Outline)

Ngữ cảnh mô tả những gì ĐÃ xảy ra trong truyện.

Nhiệm vụ của bạn:
→ Viết nội dung truyện cho scene/chapter TIẾP THEO, tức là các sự kiện xảy ra SAU điểm kết thúc hiện tại.

Bạn phải:
- Bắt đầu đúng tại điểm kết thúc mới nhất của truyện
- Không viết lại hoặc mô tả lại các sự kiện đã xảy ra

---

Context Priority (ưu tiên từ cao xuống thấp):
1. Story State
2. Event Memory
3. Character Memory
4. RAG / Previous Content
5. Scene Outline (dùng để dẫn hướng, không được phá logic context)

---

Nguyên tắc bắt buộc:
- Context là nguồn sự thật duy nhất (single source of truth)
- Không được thay đổi hoặc đảo ngược sự kiện đã xảy ra
- Không được làm sai lệch trạng thái nhân vật
- Không được viết scene trong quá khứ
- Phải tuân thủ logic timeline hiện tại

---

Tuân thủ Outline:
- Viết theo đúng thứ tự các ý trong Scene Outline
- Không bỏ sót ý quan trọng
- Có thể mở rộng chi tiết, nhưng không được thêm nội dung làm thay đổi ý nghĩa outline

---

Văn phong & chất lượng:
- Viết tự nhiên, giàu cảm xúc, có chiều sâu
- Ưu tiên “show, don’t tell”
- Kết hợp:
  + hành động
  + đối thoại
  + miêu tả nội tâm
- Tránh lặp lại thông tin đã có trong context
- Tránh lan man, giữ nhịp truyện tốt

---

Kiểm soát nội dung:
- Không tự ý thêm:
  + sức mạnh mới
  + nhân vật mới quan trọng
  + plot twist lớn
→ trừ khi đã được chuẩn bị trong context hoặc outline

---

Ngôn ngữ:
- Nếu truyện là tiếng Việt → viết hoàn toàn bằng tiếng Việt
- Nếu truyện là tiếng Anh → viết bằng tiếng Anh
- Không trộn ngôn ngữ

---

Checklist (PHẢI tự kiểm tra trước khi trả kết quả):
1. Tên nhân vật đúng 100% như context (không dịch, không đổi)
2. Không sử dụng nhân vật đã chết / mất tích
3. Không đảo ngược sự kiện trong Event Memory
4. Nội dung đi theo đúng thứ tự outline
5. Mọi sự kiện đều xảy ra SAU điểm kết thúc hiện tại
6. Không lặp lại scene cũ

---

Độ dài:
Khoảng 700–900 từ (mục tiêu ~800 từ).
Không vượt quá mức này.

---

Output:
Chỉ trả về nội dung truyện (draft).
- Không markdown
- Không tiêu đề
- Không giải thích
- Không meta text
→ Chỉ nội dung mà người đọc nhìn thấy
""" + "\n\n" + ConstitutionalRules;

    private static string GetAgent2FixSystemPrompt() => """
Role:
Bạn là AI chỉnh sửa truyện (Story Fixer).

---

Task:
Bạn nhận được:
- Draft hiện tại
- Danh sách lỗi (violations) từ Consistency Checker
- Context gốc

Nhiệm vụ:
→ CHỈ sửa những phần bị lỗi
→ KHÔNG viết lại toàn bộ

---

Nguyên tắc:
- Giữ nguyên tối đa nội dung đúng
- Chỉ chỉnh sửa đoạn liên quan đến lỗi
- Không thêm nội dung mới không cần thiết
- Không thay đổi cấu trúc scene nếu không bắt buộc

---

Cách sửa:
Với mỗi violation:
1. Xác định đoạn bị lỗi
2. Sửa đúng theo context
3. Đảm bảo không tạo lỗi mới

---

Ưu tiên sửa theo mức độ:
1. character (cao nhất)
2. timeline
3. event
4. logic
5. outline

---

Output:
Trả về bản draft ĐÃ SỬA HOÀN CHỈNH (full text)

Không giải thích
Không markdown
""" + "\n\n" + ConstitutionalRules;

    private async Task<string> RunAgent2FixAsync(
        ChatClient client,
        string contextBlock,
        string outline,
        string currentDraft,
        List<ReviewViolation> violations,
        string? feedback,
        string languageInstruction,
        CancellationToken ct)
    {
        var violationsJson = JsonSerializer.Serialize(violations.Select(v => new
        {
            type = v.Type,
            severity = v.Severity,
            quote = v.Quote,
            fix = v.Fix
        }));

        var userPrompt =
            $"{DbContextLabel}\n\n{contextBlock}\n\n---\n{Agent2Checklist}\n---\nDàn ý:\n{outline}\n\nBản nháp hiện tại:\n{currentDraft}\n\n" +
            $"Vi phạm cần sửa (JSON):\n{violationsJson}\n\n" +
            (string.IsNullOrWhiteSpace(feedback) ? "" : $"Góp ý tổng quát:\n{feedback}\n\n") +
            $"{languageInstruction}\n\nChỉ sửa đúng các lỗi được liệt kê. Trả về toàn bộ bản nháp sau khi sửa.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent2FixSystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentWriter);
        var completion = await client.CompleteChatAsync(messages, options);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent sửa lỗi không trả về nội dung.");
        return text.Trim();
    }

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
Role:
Bạn là AI kiểm tra tính nhất quán (Consistency Checker), chịu trách nhiệm phát hiện lỗi logic, mâu thuẫn và sai lệch giữa Draft với Context và Outline.

---

Input:
Bạn nhận được:
- Database Context (Story Information, RAG, Character Memory, Event Memory, Story State)
- Scene Outline
- Draft (nội dung truyện)

Ngữ cảnh mô tả những gì ĐÃ xảy ra trong truyện.

---

Context Priority (ưu tiên từ cao xuống thấp):
1. Story State (trạng thái hiện tại)
2. Event Memory (timeline sự kiện)
3. Character Memory (trạng thái nhân vật)
4. RAG / Previous Content
5. Scene Outline (chỉ để kiểm tra flow, không override context)

Nếu có mâu thuẫn → tuân theo thứ tự này.

---

Nhiệm vụ:
Phát hiện các lỗi giữa Draft với:
- Context (quan trọng nhất)
- Outline (để kiểm tra flow)

CHỈ tập trung vào logic và consistency.
KHÔNG đánh giá văn phong, không đánh giá hay/dở.

---

Nguyên tắc đánh giá:
- CHỈ đánh dấu CRITICAL khi có bằng chứng rõ ràng từ context
- Nếu không chắc chắn → đánh MINOR
- Không suy diễn ngoài dữ liệu được cung cấp
- Ưu tiên tránh false positive (reject nhầm)

---

Các loại kiểm tra:

1. Character (Nhân vật)
- Tên phải KHỚP 100% với context (không dịch, không thay đổi)
- Không dùng nhân vật đã chết / mất tích
- Không làm sai trạng thái nhân vật (quan hệ, tính cách nếu có trong memory)

---

2. Timeline Position (QUAN TRỌNG NHẤT)
- Draft phải diễn ra SAU điểm kết thúc hiện tại
- Không viết lại sự kiện đã xảy ra
- Không mô tả quá khứ như hiện tại

---

3. Event Consistency
- Không đảo ngược hoặc phủ định Event Memory
- Không tạo mâu thuẫn với timeline

---

4. Story Flow
- Tiếp nối hợp lý từ đoạn gần nhất
- Không nhảy cảnh vô lý

---

5. Outline Alignment
- Bám theo Scene Outline
- Không bỏ sót ý chính
- Không làm sai ý nghĩa

(Nếu lệch nhẹ → MINOR)

---

6. World Rules
- Không phá vỡ luật thế giới đã thiết lập

---

7. Logic tổng thể
- Không có mâu thuẫn nội tại
- Không có chi tiết phi lý so với context

---

Phân loại mức độ lỗi:

CRITICAL:
- Sai tên nhân vật (ví dụ: Xuân → Spring)
- Nhân vật chết vẫn xuất hiện
- Viết lại / đảo ngược sự kiện đã xảy ra
- Viết sai timeline (quá khứ thay vì hiện tại)
- Mâu thuẫn trực tiếp với Story State / Event Memory

MINOR:
- Diễn đạt mơ hồ
- Flow chưa mượt
- Lệch nhẹ outline
- Chi tiết chưa rõ nhưng không sai

---

Xử lý thông minh (QUAN TRỌNG):
- Nếu lỗi có thể sửa đơn giản → cung cấp fixSuggestion rõ ràng
- Không yêu cầu viết lại toàn bộ nếu không cần thiết
- Feedback phải ngắn gọn, actionable

Xử lý plot twist theo ý tác giả:
- Nếu Ý TƯỞNG TÁC GIẢ yêu cầu một plot twist/retcon có vẻ “mâu thuẫn” với context (ví dụ nhân vật đã hy sinh nhưng tác giả muốn họ trở lại):
  - Không tự động đánh CRITICAL chỉ vì mâu thuẫn đó.
  - Chỉ đánh CRITICAL nếu bản nháp KHÔNG đưa ra “cầu nối/giải thích hợp lý” để plot twist trở nên nhất quán với thế giới truyện.
  - Nếu có thể cứu bằng cách thêm 1–3 câu/đoạn ngắn giải thích: trả về violation với `fix` hướng dẫn thêm giải thích tối thiểu (targeted fix).

---

Kết luận:
- approved = false → nếu có ít nhất 1 lỗi CRITICAL
- approved = true → nếu chỉ có MINOR hoặc không có lỗi

---

Ngôn ngữ:
- Nếu truyện là tiếng Việt → output tiếng Việt
- Nếu truyện là tiếng Anh → output tiếng Anh
- Không trộn ngôn ngữ

---

Output (JSON ONLY – bắt buộc):

Trường hợp không có lỗi critical:
{ "approved": true }

Trường hợp có lỗi:
{
  "approved": false,
  "feedback": "Mô tả ngắn gọn lỗi nghiêm trọng để sửa",
  "violations": [
    {
      "type": "timeline|character|event|world_rules|logic|story_flow|outline|other",
      "quote": "đoạn trích liên quan (nếu có)",
      "severity": "critical" | "minor",
      "fix": "cách sửa cụ thể, ngắn gọn"
    }
  ]
}
""" + "\n\n" + ConstitutionalRules;

    private async Task<ReviewResult> RunAgent3ReviewAsync(ChatClient client, string contextBlock, string outline, string draft, string authorIdea, string languageInstruction, CancellationToken ct)
    {
        var userPrompt =
            $"{DbContextLabel}\n\n{contextBlock}\n\n---\nÝ tưởng tác giả (có thể trống):\n{authorIdea}\n\n---\nDàn ý:\n{outline}\n\nBản nháp:\n{draft}\n\n{languageInstruction}\n\nTrả lời bằng tiếng Việt nếu truyện tiếng Việt. Chỉ output một JSON duy nhất (approved, feedback, violations), không markdown hay giải thích.";
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent3SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentConsistencyChecker);
        var completion = await client.CompleteChatAsync(messages, options);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            return new ReviewResult(false, "Không đọc được kết quả kiểm duyệt.", new List<ReviewViolation>());
        return ParseReviewResult(text);
    }

    private static ReviewResult ParseReviewResult(string text)
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
            var violations = new List<ReviewViolation>();
            if (root.TryGetProperty("violations", out var v) && v.ValueKind == JsonValueKind.Array && v.GetArrayLength() > 0)
            {
                var hasCritical = false;
                foreach (var item in v.EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var t) ? t.GetString() : null;
                    var quote = item.TryGetProperty("quote", out var q) ? q.GetString() : null;
                    var severity = item.TryGetProperty("severity", out var sev) ? sev.GetString() : null;
                    var fix = item.TryGetProperty("fix", out var fx) ? fx.GetString() : null;
                    if (string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase))
                        hasCritical = true;
                    if (!string.IsNullOrWhiteSpace(type) && !string.IsNullOrWhiteSpace(severity))
                        violations.Add(new ReviewViolation(type!, severity!, quote, fix));
                }
                // Chỉ fail khi có ít nhất một violation critical; nếu toàn minor thì coi là đạt
                if (!hasCritical)
                    approved = true;
            }
            return new ReviewResult(approved, feedback, violations);
        }
        catch
        {
            return new ReviewResult(false, "Định dạng phản hồi kiểm duyệt không hợp lệ.", new List<ReviewViolation>());
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
