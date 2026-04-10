using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Đồng sáng tác: Dàn ý → Viết → (tùy chọn) tự sửa từ cấm + chính tả bằng Agent 2 → guardrail/chính tả lần cuối; có thể mở rộng độ dài tối thiểu.</summary>
public class AICoCreationService : IAICoCreationService
{
    private const int MinDraftWordCount = 500;
    private const string ActionOutline = "CO_CREATE_OUTLINE";
    private const string ActionWrite = "CO_CREATE_WRITE";
    private const string ActionWriteCorrect = "CO_CREATE_WRITE_CORRECT";

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
    private readonly IChapterCheckService _chapterCheck;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IConfiguration _configuration;

    public AICoCreationService(
        IStoryRepository storyRepository,
        IChapterRepository chapterRepository,
        IAiGeneratedContentRepository aiContentRepository,
        IStoryMemoryEngine memoryEngine,
        IContentGuardrailService guardrail,
        IChapterCheckService chapterCheck,
        IAIUsageLogRepository aiUsageLogRepository,
        IConfiguration configuration)
    {
        _storyRepository = storyRepository;
        _chapterRepository = chapterRepository;
        _aiContentRepository = aiContentRepository;
        _memoryEngine = memoryEngine;
        _guardrail = guardrail;
        _chapterCheck = chapterCheck;
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

        var allChaptersOrdered = _chapterRepository.GetByStoryId(request.StoryId).OrderBy(c => c.order_index).ToList();
        var targetOrderForWarning = ResolveCoCreateTargetOrderIndex(request, allChaptersOrdered);
        var contextWarning = ChapterAiContextWarningHelper.GetWarningIfApplicable(allChaptersOrdered, targetOrderForWarning);

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

        string? ideaConflictWarning = null;
        var ideaFeedback = TryParseIdeaContradiction(outlineJson);
        if (ideaFeedback != null)
        {
            ideaConflictWarning = ideaFeedback;
            progress?.Report(new CoCreateProgressEvent
            {
                Step = "Outline_Conflict_Adjust",
                DurationMs = 0,
                Message = "Phát hiện xung đột ý tưởng; đang điều chỉnh để vẫn viết tiếp."
            });
            outlineJson = await RunAgent1OutlineAsync(
                clientPlanner,
                story,
                contextBlock,
                effectiveIdea,
                languageInstruction,
                cancellationToken,
                allowContradictionResponse: false);

            // Nếu model vẫn trả nhánh contradiction thì fallback dàn ý tối thiểu để không dừng luồng.
            if (TryParseIdeaContradiction(outlineJson) != null)
                outlineJson = BuildForcedOutlineFallbackJson(effectiveIdea);
        }

        var (suggestedTitle, outlineBody, charactersFromPayload) = TryExtractOutlineAndSuggestedTitle(outlineJson);
        suggestedTitle = EnsureUniqueSuggestedTitle(suggestedTitle, allChaptersOrdered);
        var outlineForPrompt = FormatOutlineForPrompt(outlineBody);
        var charactersInvolved = (charactersFromPayload?.Count ?? 0) > 0
            ? charactersFromPayload!
            : ExtractCharactersInvolved(outlineForPrompt);

        var (p2, m2, k2, u2) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentWriter);
        var clientWriter = AIClientHelper.CreateChatClient(p2, m2, k2, u2);

        var swWrite = Stopwatch.StartNew();
        var draft = await RunAgent2WriteAsync(clientWriter, contextBlock, outlineForPrompt, languageInstruction, suggestedTitle, cancellationToken);
        swWrite.Stop();
        draft = StripTrailingFeedbackFromDraft(draft);
        durations.Add(new AgentDuration { Step = "Write", DurationMs = swWrite.ElapsedMilliseconds });
        progress?.Report(new CoCreateProgressEvent { Step = "Write", DurationMs = swWrite.ElapsedMilliseconds, Message = "Đã viết nội dung" });
        LogUsage(authorUserId, request.StoryId, null, ActionWrite, m2, 0, 0);

        var (draftAfterRefine, approved, reviewFeedback, revisionCount) = await RefineDraftWithSelfCorrectionAsync(
            clientWriter,
            request.StoryId,
            authorUserId,
            contextBlock,
            outlineForPrompt,
            languageInstruction,
            draft,
            m2,
            cancellationToken,
            progress,
            durations,
            phaseLabel: "");
        draft = draftAfterRefine;

        // Enforce minimum length with a single lightweight expansion pass to avoid many rewrites.
        if (CountWords(draft) < MinDraftWordCount)
        {
            var swExpand = Stopwatch.StartNew();
            draft = await RunAgent2ExpandAsync(
                clientWriter,
                contextBlock,
                outlineForPrompt,
                draft,
                languageInstruction,
                suggestedTitle,
                cancellationToken);
            swExpand.Stop();
            draft = StripTrailingFeedbackFromDraft(draft);
            durations.Add(new AgentDuration { Step = "Length_Expand", DurationMs = swExpand.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = "Length_Expand", DurationMs = swExpand.ElapsedMilliseconds, Message = "Đã mở rộng nội dung để đạt độ dài tối thiểu" });
            LogUsage(authorUserId, request.StoryId, null, ActionWrite, m2, 0, 0);

            var (draftExpanded, expandApproved, expandReviewFeedback, expandRewrites) = await RefineDraftWithSelfCorrectionAsync(
                clientWriter,
                request.StoryId,
                authorUserId,
                contextBlock,
                outlineForPrompt,
                languageInstruction,
                draft,
                m2,
                cancellationToken,
                progress,
                durations,
                phaseLabel: "Length_Expand_");
            draft = draftExpanded;
            revisionCount += expandRewrites;
            if (!expandApproved)
            {
                approved = false;
                reviewFeedback = expandReviewFeedback;
            }
        }

        var finalWordCount = CountWords(draft);
        if (finalWordCount < MinDraftWordCount)
            throw new InvalidOperationException($"Nội dung AI tạo ra quá ngắn ({finalWordCount} từ), yêu cầu phải lớn hơn 500 từ. Vui lòng thử lại.");

        var saved = SaveAiGeneratedContentOnly(
            request.StoryId,
            authorUserId,
            hasAuthorIdea ? rawIdea! : "[AUTO] Tiếp tục theo mạch truyện (không có gợi ý tác giả)",
            draft,
            request.ChapterOrderIndex,
            request.ChapterId);
        return new CoCreationResponse
        {
            Outline = outlineForPrompt,
            SuggestedChapterTitle = suggestedTitle,
            CharactersInvolved = charactersInvolved,
            IdeaConflictWarning = ideaConflictWarning,
            FinalContent = draft,
            Approved = approved,
            RevisionCount = revisionCount,
            RevisionFeedbacks = null,
            ReviewFeedback = approved ? null : reviewFeedback,
            ChapterId = saved.ChapterId,
            AiGeneratedContentId = saved.Id,
            ChapterIndex = saved.ChapterIndex,
            AgentDurations = durations.Count > 0 ? durations : null,
            ContextWarning = contextWarning
        };
    }

    private static string? EnsureUniqueSuggestedTitle(string? suggestedTitle, IReadOnlyList<chapters> chaptersOrdered)
    {
        if (string.IsNullOrWhiteSpace(suggestedTitle))
            return suggestedTitle;

        var reserved = chaptersOrdered
            .Select(c => c.title?.Trim())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var baseTitle = suggestedTitle.Trim();
        if (reserved.Add(baseTitle))
            return baseTitle;

        var n = 2;
        while (true)
        {
            var candidate = $"{baseTitle} ({n})";
            if (reserved.Add(candidate))
                return candidate;
            n++;
        }
    }

    private static int ResolveCoCreateTargetOrderIndex(CoCreationRequest request, List<chapters> allOrdered)
    {
        if (request.ChapterOrderIndex is int coIdx && coIdx >= 0)
            return coIdx;
        if (request.ChapterId.HasValue && request.ChapterId.Value != Guid.Empty)
        {
            var ch = allOrdered.FirstOrDefault(c => c.id == request.ChapterId.Value);
            if (ch != null)
                return ch.order_index;
        }

        return allOrdered.Count == 0 ? 0 : allOrdered.Max(c => c.order_index) + 1;
    }

    /// <summary>Chỉ lưu bản <see cref="ai_generated_content"/> (không tạo/cập nhật <see cref="chapters"/>).
    /// <see cref="ai_generated_content.chapter_index"/> = <paramref name="targetOrderIndex"/> nếu hợp lệ; ngược lại = slot chương tiếp theo.</summary>
    private (Guid? Id, int? ChapterIndex, Guid? ChapterId) SaveAiGeneratedContentOnly(
        Guid storyId,
        Guid authorUserId,
        string authorIdea,
        string finalContent,
        int? targetOrderIndex = null,
        Guid? chapterId = null)
    {
        if (string.IsNullOrWhiteSpace(finalContent)) return (null, null, null);
        var chaptersList = _chapterRepository.GetByStoryId(storyId).ToList();
        chapters? targetChapter = null;
        if (chapterId.HasValue && chapterId.Value != Guid.Empty)
        {
            targetChapter = _chapterRepository.GetById(chapterId.Value);
            if (targetChapter != null && targetChapter.story_id != storyId)
                throw new InvalidOperationException("ChapterId không khớp truyện.");
        }
        // Khớp chapters.order_index từ FE (chương 1 → order_index 0). Ưu tiên index chương đang soạn để map đúng slot chương hiện tại.
        int nextChapterIndex;
        if (targetOrderIndex is >= 0)
            nextChapterIndex = targetOrderIndex.Value;
        else if (targetChapter != null)
            nextChapterIndex = targetChapter.order_index;
        else
            nextChapterIndex = chaptersList.Count == 0 ? 0 : chaptersList.Max(c => c.order_index) + 1;
        var now = DateTime.UtcNow;
        var aiRecord = new ai_generated_content
        {
            id = Guid.NewGuid(),
            story_id = storyId,
            chapter_id = targetChapter?.id,
            draft_chapter_id = targetChapter == null ? chapterId : null,
            chapter_index = nextChapterIndex,
            user_id = authorUserId,
            input_prompt = authorIdea.Length > 2000 ? authorIdea[..2000] + "..." : authorIdea,
            ai_output = finalContent,
            created_at = now
        };
        _aiContentRepository.Add(aiRecord);
        return (aiRecord.id, nextChapterIndex, targetChapter?.id ?? chapterId);
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

Output Format (bắt buộc):

1) Khi CHẤP NHẬN (không từ chối vì mâu thuẫn):
→ Trả về DUY NHẤT một JSON hợp lệ, không markdown, không ký tự ngoài JSON.
Cấu trúc:
{
  "suggestedChapterTitle": "Một dòng tiêu đề gợi ý (ngắn, gợi tình tiết; không prefix kiểu \"Chương 1\" hay \"Chapter 1\")",
  "outline": "Toàn bộ dàn ý: Scene Objective, Scene Outline (2–7 ý), Characters Involved, Potential Conflict, Expected Outcome — dùng \\n trong chuỗi JSON để xuống dòng giữa các mục.",
  "charactersInvolved": ["Tên nhân vật 1", "Tên nhân vật 2"]
}
Trường suggestedChapterTitle bắt buộc (chuỗi, có thể rỗng "" nếu không đặt được tiêu đề ngắn).
Trường outline bắt buộc, nội dung đúng cấu trúc Scene như các mục Scene Objective / Scene Outline / … đã nêu phía trên.
Trường charactersInvolved bắt buộc: mảng tên nhân vật (không thêm mô tả như "để tạo...", "hook", v.v.).

2) Khi TỪ CHỐI (ideaContradiction):
→ Chỉ trả về: { "ideaContradiction": true, "feedback": "Giải thích ngắn gọn bằng tiếng Việt." }

""" + "\n\n" + ConstitutionalRules;

    private static string GetAgent2CorrectSystemPrompt() => """
Role:
Bạn là AI viết truyện (Story Writer) — lần này nhiệm vụ là SỬA bản nháp đã có theo yêu cầu cụ thể.

Quy tắc:
- Thực hiện đầy đủ các sửa được liệt kê (từ cấm → thay bằng diễn đạt tương đương; chính tả → thay cụm sai bằng gợi ý đúng).
- Không đổi cốt truyện, timeline, nhân vật so với bản nháp và dàn ý; không thêm plot twist lớn.
- Giữ ngôn ngữ (Việt/Anh) như bản gốc; độ dài không được ngắn hơn đáng kể so với bản nháp (nếu bản đã ≥500 từ thì bản sau sửa cũng ≥500 từ).
- Chỉ trả về toàn bộ văn bản sau sửa, không markdown, không giải thích.
""" + "\n\n" + ConstitutionalRules;

    private static string? BuildCorrectionInstructionBlock(GuardrailResult gr, CheckChapterResponse spell)
    {
        var parts = new List<string>();
        if (!gr.Passed)
        {
            if (gr.Violations.Count > 0)
            {
                parts.Add("— Từ/cụm không được phép (phải loại bỏ hoặc thay thế, giữ ngữ cảnh):");
                foreach (var v in gr.Violations)
                {
                    var q = string.IsNullOrWhiteSpace(v.Quote) ? "" : $" (tránh dùng: «{v.Quote}»)";
                    parts.Add($"  • [{v.Type}] {v.Message}{q}");
                }
            }
            else
                parts.Add("— Nội dung có thể chứa từ cấm: hãy thay các cụm nhạy cảm bằng diễn đạt tương đương, giữ ngữ cảnh.");
        }
        if (!spell.Passed && spell.SpellingIssues.Count > 0)
        {
            parts.Add("— Lỗi chính tả (thay cụm trái bằng cụm phải, giữ nguyên câu chữ xung quanh):");
            var take = spell.SpellingIssues.Take(40).ToList();
            foreach (var s in take)
                parts.Add($"  • «{s.WordOrPhrase}» → «{s.Suggestion}»");
            if (spell.SpellingIssues.Count > 40)
                parts.Add($"  • (và {spell.SpellingIssues.Count - 40} lỗi khác — sửa tương tự)");
        }
        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    private async Task<string> RunAgent2CorrectAsync(
        ChatClient client,
        string contextBlock,
        string outline,
        string currentDraft,
        string languageInstruction,
        string correctionBlock,
        CancellationToken ct)
    {
        var userPrompt =
            $"{DbContextLabel}\n\n{contextBlock}\n\n---\n{Agent2Checklist}\n---\nDàn ý (không đổi hướng plot):\n{outline}\n\n" +
            "=== Bản nháp hiện tại ===\n" + currentDraft + "\n\n" +
            "=== Yêu cầu sửa (bắt buộc) ===\n" + correctionBlock + "\n\n" +
            $"{languageInstruction}\n\n" +
            "Viết lại TOÀN BỘ bản nháp sau khi đã áp dụng các sửa trên. Giữ mạch truyện và tone. " +
            "Chỉ output nội dung truyện (văn bản), không markdown hay giải thích.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent2CorrectSystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentWriter);
        var completion = await client.CompleteChatAsync(messages, options, ct);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent sửa bản nháp không trả về nội dung.");
        return text.Trim();
    }

    /// <summary>Lặp tối đa N lần: kiểm tra từ cấm + chính tả → nếu lỗi thì Agent 2 viết lại.</summary>
    private async Task<(string Draft, bool Approved, string? ReviewFeedback, int RewritesUsed)> RefineDraftWithSelfCorrectionAsync(
        ChatClient clientWriter,
        Guid storyId,
        Guid authorUserId,
        string contextBlock,
        string outlineForPrompt,
        string languageInstruction,
        string draft,
        string writerModelName,
        CancellationToken ct,
        IProgress<CoCreateProgressEvent>? progress,
        List<AgentDuration> durations,
        string phaseLabel)
    {
        var enable = _configuration.GetValue("AI:CoCreateEnableSelfCorrection", true);
        var maxRounds = _configuration.GetValue("AI:CoCreateSelfCorrectionMaxRounds", _configuration.GetValue("AI:CoCreateMaxRevisions", 2));
        if (maxRounds < 0) maxRounds = 0;
        if (maxRounds > 5) maxRounds = 5;

        var guardStepName = string.IsNullOrEmpty(phaseLabel) ? "Guardrail" : "Length_Expand_Guardrail";

        if (!enable || maxRounds == 0)
        {
            var sw = Stopwatch.StartNew();
            var gr = await _guardrail.CheckAsync(storyId, draft, ct);
            sw.Stop();
            durations.Add(new AgentDuration { Step = guardStepName, DurationMs = sw.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = guardStepName, DurationMs = sw.ElapsedMilliseconds, Message = "Đã kiểm tra từ cấm" });
            var ok = gr.Passed;
            var fb = ok ? null : string.Join(" ", gr.Violations.Select(v => $"[{v.Type}] {v.Message}"));
            return (draft, ok, fb, 0);
        }

        var rewrites = 0;
        for (var round = 0; round < maxRounds; round++)
        {
            var prefix = $"{phaseLabel}SelfCorrect_R{round + 1}";

            var swG = Stopwatch.StartNew();
            var gr = await _guardrail.CheckAsync(storyId, draft, ct);
            swG.Stop();
            durations.Add(new AgentDuration { Step = $"{prefix}_Banned", DurationMs = swG.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"{prefix}_Banned", DurationMs = swG.ElapsedMilliseconds, Message = "Đang kiểm tra từ cấm" });

            var swS = Stopwatch.StartNew();
            var spell = await _chapterCheck.CheckSpellingOnlyAsync(
                new CheckChapterSpellingRequest { Content = draft, StoryId = storyId },
                userId: null,
                ct);
            swS.Stop();
            durations.Add(new AgentDuration { Step = $"{prefix}_Spell", DurationMs = swS.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"{prefix}_Spell", DurationMs = swS.ElapsedMilliseconds, Message = "Đang kiểm tra chính tả" });

            if (gr.Passed && spell.Passed)
            {
                progress?.Report(new CoCreateProgressEvent { Step = guardStepName, DurationMs = 0, Message = "Đã đạt từ cấm và chính tả" });
                return (draft, true, null, rewrites);
            }

            var instr = BuildCorrectionInstructionBlock(gr, spell);
            if (string.IsNullOrWhiteSpace(instr))
                break;

            var swW = Stopwatch.StartNew();
            draft = await RunAgent2CorrectAsync(clientWriter, contextBlock, outlineForPrompt, draft, languageInstruction, instr, ct);
            swW.Stop();
            draft = StripTrailingFeedbackFromDraft(draft);
            rewrites++;
            durations.Add(new AgentDuration { Step = $"{prefix}_Rewrite", DurationMs = swW.ElapsedMilliseconds });
            progress?.Report(new CoCreateProgressEvent { Step = $"{prefix}_Rewrite", DurationMs = swW.ElapsedMilliseconds, Message = $"Agent 2 đang viết lại bản nháp (lần {rewrites})" });
            LogUsage(authorUserId, storyId, null, ActionWriteCorrect, writerModelName, 0, 0);
        }

        var swGf = Stopwatch.StartNew();
        var grFinal = await _guardrail.CheckAsync(storyId, draft, ct);
        swGf.Stop();
        durations.Add(new AgentDuration { Step = $"{phaseLabel}SelfCorrect_Final_Banned", DurationMs = swGf.ElapsedMilliseconds });

        var swSf = Stopwatch.StartNew();
        var spellFinal = await _chapterCheck.CheckSpellingOnlyAsync(
            new CheckChapterSpellingRequest { Content = draft, StoryId = storyId },
            userId: null,
            ct);
        swSf.Stop();
        durations.Add(new AgentDuration { Step = $"{phaseLabel}SelfCorrect_Final_Spell", DurationMs = swSf.ElapsedMilliseconds });

        var okFinal = grFinal.Passed && spellFinal.Passed;
        string? review = null;
        if (!okFinal)
        {
            var bits = new List<string>();
            if (!grFinal.Passed)
                bits.Add(string.Join(" ", grFinal.Violations.Select(v => $"[{v.Type}] {v.Message}")));
            if (!spellFinal.Passed)
                bits.Add("Chính tả: " + string.Join("; ", spellFinal.SpellingIssues.Take(12).Select(s => $"{s.WordOrPhrase}→{s.Suggestion}")));
            review = string.Join(" ", bits);
        }

        progress?.Report(new CoCreateProgressEvent { Step = guardStepName, DurationMs = 0, Message = okFinal ? "Hoàn tất kiểm tra" : "Còn lỗi sau khi hết số lần sửa tự động" });
        return (draft, okFinal, review, rewrites);
    }

    private async Task<string> RunAgent1OutlineAsync(
        ChatClient client,
        stories story,
        string contextBlock,
        string authorIdea,
        string languageInstruction,
        CancellationToken ct,
        bool allowContradictionResponse = true)
    {
        var storyInfo = $"Story Information:\nTitle: {story.title}\nSummary: {story.summary ?? ""}\n\nUser Idea:\n{authorIdea}";
        var contradictionInstruction = allowContradictionResponse
            ? "Chỉ trả JSON ideaContradiction khi mâu thuẫn RÕ RÀNG với context (Character/Event Memory)."
            : "KHÔNG được trả ideaContradiction. Dù có xung đột, hãy tự điều chỉnh ý tưởng để vẫn tạo JSON suggestedChapterTitle + outline hợp lệ.";
        var userPrompt = $"{storyInfo}\n\n---\n{DbContextLabel}\n\n{contextBlock}\n\n---\n{languageInstruction}\n\nNgữ cảnh trên là phần truyện ĐÃ XẢY RA. Chỉ sinh outline cho phần TIẾP THEO (sau điểm kết thúc hiện tại). Trả lời bằng cùng ngôn ngữ truyện. Khi chấp nhận: trả về đúng một JSON có suggestedChapterTitle + outline theo system prompt. {contradictionInstruction}";
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

    private static string BuildForcedOutlineFallbackJson(string effectiveIdea)
    {
        var idea = (effectiveIdea ?? "").Trim();
        if (idea.Length > 260) idea = idea[..260] + "...";
        var outline = "Scene Objective: Tiếp tục mạch truyện theo ý tưởng tác giả đã được điều chỉnh cho phù hợp ngữ cảnh.\n"
            + "Scene Outline:\n"
            + $"- Khởi đầu từ điểm kết thúc hiện tại và mở ra tình huống mới theo ý tưởng: {idea}\n"
            + "- Đẩy xung đột theo hướng nhất quán với Character/Event Memory.\n"
            + "- Kết thúc bằng một câu hỏi mở cho chương kế tiếp.\n"
            + "Characters Involved: Nhân vật chính của mạch truyện hiện tại.\n"
            + "Potential Conflict: Mâu thuẫn giữa mục tiêu cá nhân và sự thật mới được hé lộ.\n"
            + "Expected Outcome: Tạo đà tự nhiên cho diễn biến tiếp theo.";
        return JsonSerializer.Serialize(new
        {
            suggestedChapterTitle = "Chương tiếp theo",
            outline,
            charactersInvolved = new[] { "Nhân vật chính" }
        });
    }

    private async Task<string> RunAgent2ExpandAsync(
        ChatClient client,
        string contextBlock,
        string outline,
        string currentDraft,
        string languageInstruction,
        string? suggestedChapterTitle,
        CancellationToken ct)
    {
        var titleHint = string.IsNullOrWhiteSpace(suggestedChapterTitle)
            ? ""
            : $"\n\n(Tiêu đề chương gợi ý — chỉ tham khảo tone/nội dung; không in tiêu đề trong văn bản: «{suggestedChapterTitle}»)";
        var userPrompt =
            $"{DbContextLabel}\n\n{contextBlock}\n\n---\n{Agent2Checklist}\n---\nDàn ý:\n{outline}\n\nBản nháp hiện tại:\n{currentDraft}{titleHint}\n\n" +
            $"{languageInstruction}\n\n" +
            "Yêu cầu: giữ nguyên mạch truyện, sự kiện chính và tính nhất quán của bản nháp hiện tại; chỉ mở rộng thêm chi tiết hợp lý (miêu tả, nội tâm, đối thoại, chuyển cảnh) để bản đầy đủ trên 500 từ, ưu tiên khoảng 600–700 từ. " +
            "Không viết lại theo hướng khác, không thêm plot twist lớn. Trả về toàn bộ bản nháp hoàn chỉnh, chỉ nội dung truyện.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent2SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentWriter);
        var completion = await client.CompleteChatAsync(messages, options);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent mở rộng nội dung không trả về nội dung.");
        return text.Trim();
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
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

    /// <summary>Trích tiêu đề gợi ý, outline và danh sách nhân vật từ JSON Agent 1; fallback toàn bộ chuỗi nếu không parse được (tương thích cũ).</summary>
    private static (string? SuggestedTitle, string OutlineBody, List<string>? CharactersInvolved) TryExtractOutlineAndSuggestedTitle(string outlineRaw)
    {
        var raw = outlineRaw.Trim();
        if (raw.Length == 0)
            return (null, outlineRaw, null);

        var toParse = raw;
        if (toParse.StartsWith("```", StringComparison.Ordinal))
        {
            var start = toParse.IndexOf('\n') + 1;
            var end = toParse.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                toParse = toParse[start..end].Trim();
        }

        try
        {
            using var doc = JsonDocument.Parse(toParse);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, outlineRaw, null);

            if (root.TryGetProperty("ideaContradiction", out var ic) && ic.ValueKind == JsonValueKind.True)
                return (null, outlineRaw, null);

            if (!root.TryGetProperty("outline", out var outlineEl) || outlineEl.ValueKind != JsonValueKind.String)
                return (null, outlineRaw, null);

            var outlineText = NormalizeOutlineText(outlineEl.GetString());
            if (outlineText.Length == 0)
                return (null, outlineRaw, null);

            string? title = null;
            if (root.TryGetProperty("suggestedChapterTitle", out var st) && st.ValueKind == JsonValueKind.String)
                title = NormalizeSuggestedTitle(st.GetString());
            else if (root.TryGetProperty("suggested_chapter_title", out var st2) && st2.ValueKind == JsonValueKind.String)
                title = NormalizeSuggestedTitle(st2.GetString());

            var chars = TryReadCharactersFromRoot(root);
            return (title, outlineText, chars);
        }
        catch
        {
            return (null, outlineRaw, null);
        }
    }

    private static List<string>? TryReadCharactersFromRoot(JsonElement root)
    {
        static IEnumerable<string> keys()
            => new[] { "charactersInvolved", "characters_involved", "characters", "cast", "nhanVatThamGia", "nhan_vat_tham_gia" };
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var key in keys())
        {
            if (!root.TryGetProperty(key, out var prop)) continue;
            if (prop.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in prop.EnumerateArray())
                {
                    var s = item.ValueKind == JsonValueKind.String ? item.GetString() : null;
                    if (!IsLikelyCharacterToken(s)) continue;
                    var t = s!.Trim();
                    if (seen.Add(t)) result.Add(t);
                }
            }
            else if (prop.ValueKind == JsonValueKind.String)
            {
                foreach (var part in (prop.GetString() ?? "").Split(new[] { ',', ';', '/', '|', '，' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var s = part.Trim();
                    if (!IsLikelyCharacterToken(s)) continue;
                    if (seen.Add(s)) result.Add(s);
                }
            }
        }
        return result.Count > 0 ? result : null;
    }

    private static string? NormalizeSuggestedTitle(string? s)
    {
        if (string.IsNullOrWhiteSpace(s))
            return null;
        var parts = s.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var t = string.Join(' ', parts).Trim();
        if (t.Length > 200)
            t = t[..200].TrimEnd();
        return t.Length == 0 ? null : t;
    }

    private static List<string> ExtractCharactersInvolved(string outline)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddName(string? value)
        {
            var t = (value ?? "").Trim().Trim('-', '*', '•', '"', '\'', '.', '!', '?', ':', ';');
            if (!IsLikelyCharacterToken(t)) return;
            if (seen.Add(t))
                result.Add(t);
        }

        void AddFromCsv(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            foreach (var part in value.Split(new[] { ',', ';', '/', '|', '，' }, StringSplitOptions.RemoveEmptyEntries))
                AddName(part);
        }

        bool IsCharacterLabel(string t)
        {
            return t.StartsWith("Characters Involved", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Characters", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Nhân vật tham gia", StringComparison.OrdinalIgnoreCase)
                || t.StartsWith("Nhân vật", StringComparison.OrdinalIgnoreCase);
        }

        var lines = (outline ?? string.Empty).Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0) continue;
            if (!IsCharacterLabel(t)) continue;

            var colonIdx = t.IndexOf(':');
            if (colonIdx >= 0 && colonIdx < t.Length - 1)
            {
                AddFromCsv(t[(colonIdx + 1)..]);
                continue;
            }

            // Header-only line: parse the following bullet lines.
            for (var j = i + 1; j < lines.Length; j++)
            {
                var next = lines[j].Trim();
                if (next.Length == 0) break;
                if (next.Contains(':')) break;
                if (next.StartsWith("-") || next.StartsWith("*") || next.StartsWith("•"))
                    AddName(next[1..]);
                else
                    AddName(next);
            }
        }

        return result;
    }

    private static bool IsLikelyCharacterToken(string? candidate)
    {
        var t = (candidate ?? "").Trim().Trim('.', ',', ';', ':', '!', '?', '"', '\'');
        if (t.Length < 2 || t.Length > 64)
            return false;

        if (!Regex.IsMatch(t, @"^[\p{L}\p{M} .'-]{2,64}$", RegexOptions.CultureInvariant))
            return false;

        return true;
    }

    private static string NormalizeOutlineText(string? text)
    {
        var s = (text ?? "").Trim();
        if (s.Length == 0) return string.Empty;
        s = s.Replace("\\r\\n", "\n").Replace("\\n", "\n").Replace("\\t", " ");
        s = Regex.Replace(s, @"(?m)^(\d+)\.(\S)", "$1. $2");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        return s.Trim();
    }

    /// <summary>Chuẩn hóa outline để đưa vào Agent 2: nếu là format Story Analyzer (Scene Objective, Scene Outline, ...) thì trả về nguyên bản; nếu là JSON scenes thì format lại.</summary>
    private static string FormatOutlineForPrompt(string outlineRaw)
    {
        var raw = NormalizeOutlineText(outlineRaw);
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

    /// <summary>Checklist ràng buộc cho Agent 2: đối chiếu ngữ cảnh trước khi viết.</summary>
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
7. Độ dài: trên 500 từ; ưu tiên khoảng 600–700 từ (nếu chưa đủ thì mở rộng nội dung hợp lý trước khi gửi)

---

Độ dài (bắt buộc):
- Phải trên 500 từ (đếm theo từ trong ngôn ngữ đang viết). Không được gửi bản nháp từ 500 từ trở xuống.
- Mục tiêu: khoảng 600–700 từ (ưu tiên đạt khoảng này).
- Tối đa khoảng 750 từ — tránh lan man; nếu thiếu độ dài, hãy bổ sung chi tiết, đối thoại hoặc miêu tả hợp lý thay vì kết thúc sớm.

---

Output:
Chỉ trả về nội dung truyện (draft).
- Không markdown
- Không tiêu đề
- Không giải thích
- Không meta text
→ Chỉ nội dung mà người đọc nhìn thấy
""" + "\n\n" + ConstitutionalRules;

    private async Task<string> RunAgent2WriteAsync(
        ChatClient client,
        string contextBlock,
        string outline,
        string languageInstruction,
        string? suggestedChapterTitle,
        CancellationToken ct)
    {
        var titleHint = string.IsNullOrWhiteSpace(suggestedChapterTitle)
            ? ""
            : $"\n\n(Tiêu đề chương gợi ý — chỉ tham khảo tone/nội dung; không được in tiêu đề hay dòng meta trong văn bản truyện: «{suggestedChapterTitle}»)";
        var userPrompt = $"{DbContextLabel}\n\n{contextBlock}\n\n---\n{Agent2Checklist}\n---\nDàn ý cần viết:\n{outline}{titleHint}";
        userPrompt += $"\n\n{languageInstruction}\n\nViết bằng tiếng Việt nếu truyện tiếng Việt. Độ dài: tối thiểu 500 từ, mục tiêu 600–700 từ (tối đa khoảng 750 từ). Chỉ output nội dung chương (văn bản truyện), không markdown hay giải thích.";

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

    /// <summary>Cắt bỏ đoạn "Feedback: ..." mà model đôi khi thêm vào cuối bản nháp.</summary>
    private static string StripTrailingFeedbackFromDraft(string draft)
    {
        if (string.IsNullOrWhiteSpace(draft)) return draft;
        var idx = draft.IndexOf("\n\nFeedback:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) idx = draft.IndexOf("\nFeedback:", StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
            return draft[..idx].TrimEnd();
        return draft;
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
