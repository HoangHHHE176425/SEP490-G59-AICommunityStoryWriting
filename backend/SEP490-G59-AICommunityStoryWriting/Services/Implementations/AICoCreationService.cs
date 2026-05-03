using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using Repositories;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;
using Services;

namespace Services.Implementations;

/// <summary>Đồng sáng tác: Dàn ý → Viết → (tùy chọn) tự sửa từ cấm bằng Agent 2 → guardrail lần cuối; có thể mở rộng độ dài tối thiểu.</summary>
public class AICoCreationService : IAICoCreationService
{
    private const int AuthorIdeaMaxChars = 1500;
    private const int MinDraftWordCount = 500;
    private const int ExpandTargetWords = 560;
    private const int DefaultEmbeddingRagQueryMaxChars = 10000;
    private const string ActionOutline = "CO_CREATE_OUTLINE";
    private const string ActionWrite = "CO_CREATE_WRITE";
    private const string ActionWriteCorrect = "CO_CREATE_WRITE_CORRECT";

    /// <summary>Quy tắc bắt buộc (Constitutional): đưa vào system prompt mọi agent.</summary>
  private const string ConstitutionalRules = """
Quy tắc bắt buộc:

1) Nguồn dữ liệu và mức ưu tiên
- Chỉ dùng ngữ cảnh đã cung cấp: Story Information, RAG, Character Memory, Event Memory, Story State.
- Khi User Idea là diễn biến cụ thể do tác giả đặt: lấy đó làm hướng chính cho phần TIẾP THEO, đồng thời giữ nhất quán với ngữ cảnh.
- Khi User Idea chỉ là hướng dẫn chung: ưu tiên Story State -> Event Memory -> Character Memory -> RAG.

2) Timeline
- Chỉ viết sự kiện xảy ra SAU điểm kết thúc hiện tại của truyện (mốc là sự kiện cuối cùng trong phần ngữ cảnh gần nhất).
- Không viết lại sự kiện đã xảy ra như nội dung chính của scene/chương mới; chỉ tham chiếu ngắn gọn khi thật sự cần.

3) Nhất quán và tên riêng
- Không tạo chi tiết mâu thuẫn với Story State, Event Memory, Character Memory.
- Dùng chính xác tên nhân vật/địa danh/thuật ngữ như trong ngữ cảnh; không dịch hoặc biến đổi tên riêng.

4) Kiểm soát nội dung mới
- Hạn chế thêm nhân vật quan trọng mới, sức mạnh mới, phe phái mới, plot twist lớn nếu ngữ cảnh chưa có gợi mở.
- Có thể thêm có kiểm soát khi phục vụ trực tiếp User Idea/Scene Outline hoặc giúp chuyển mạch hợp lý, nhưng không được làm mâu thuẫn Story State/Event/Character Memory.

5) Định dạng đầu ra
- Tuân thủ đúng format được yêu cầu ở từng bước.
- Không thêm ghi chú, giải thích, hoặc nội dung ngoài format.
""";

    private readonly IStoryRepository _storyRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IAiGeneratedContentRepository _aiContentRepository;
    private readonly IStoryMemoryEngine _memoryEngine;
    private readonly IStoryRagService _storyRagService;
    private readonly IContentGuardrailService _guardrail;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IConfiguration _configuration;
    private readonly IAuthorAiTokenBudgetService _authorAiTokenBudget;
    private readonly ILogger<AICoCreationService> _logger;

    public AICoCreationService(
        IStoryRepository storyRepository,
        IChapterRepository chapterRepository,
        IAiGeneratedContentRepository aiContentRepository,
        IStoryMemoryEngine memoryEngine,
        IStoryRagService storyRagService,
        IContentGuardrailService guardrail,
        IAIUsageLogRepository aiUsageLogRepository,
        IConfiguration configuration,
        IAuthorAiTokenBudgetService authorAiTokenBudget,
        ILogger<AICoCreationService> logger)
    {
        _storyRepository = storyRepository;
        _chapterRepository = chapterRepository;
        _aiContentRepository = aiContentRepository;
        _memoryEngine = memoryEngine;
        _storyRagService = storyRagService;
        _guardrail = guardrail;
        _aiUsageLogRepository = aiUsageLogRepository;
        _configuration = configuration;
        _authorAiTokenBudget = authorAiTokenBudget;
        _logger = logger;
    }

    public async Task<CoCreationResponse> CoCreateAsync(
        CoCreationRequest request,
        Guid authorUserId,
        CancellationToken cancellationToken = default)
    {
        if (request.StoryId == Guid.Empty)
            throw new ArgumentException("StoryId là bắt buộc.");

        if (authorUserId == Guid.Empty)
            throw new UnauthorizedAccessException("Không xác định được người dùng. Vui lòng đăng nhập lại.");

        var rawIdea = request.AuthorIdea?.Trim();
        if (!string.IsNullOrWhiteSpace(rawIdea) && rawIdea.Length > AuthorIdeaMaxChars)
            throw new InvalidOperationException($"Ý tưởng tác giả không được vượt quá {AuthorIdeaMaxChars} ký tự.");

        try
        {
            await _authorAiTokenBudget.EnsureWithinBudgetAsync(authorUserId, cancellationToken).ConfigureAwait(false);
        }
        catch (AuthorAiTokenBudgetExceededException ex)
        {
            _logger.LogWarning(ex, "Đã vượt hạn mức token. AuthorUserId={AuthorUserId} StoryId={StoryId}", authorUserId, request.StoryId);
            throw;
        }

        var minCoCreateTokens = _configuration.GetValue("AI:CoCreateMinRequiredTokens", 14000);
        if (minCoCreateTokens > 0)
        {
            var budgetDto = await _authorAiTokenBudget.GetBudgetAsync(authorUserId, cancellationToken).ConfigureAwait(false);
            var remaining = budgetDto?.TokensRemaining;
            if ((remaining ?? long.MaxValue) < minCoCreateTokens)
            {
                var ex = new AuthorAiEstimatedTokensInsufficientException(remaining, minCoCreateTokens);
                _logger.LogWarning(
                    "Không đủ hạn mức token tối thiểu. AuthorUserId={AuthorUserId} StoryId={StoryId} TokensRemaining={TokensRemaining} MinRequired={MinRequired}",
                    authorUserId, request.StoryId, remaining, minCoCreateTokens);
                throw ex;
            }
        }

        var story = _storyRepository.GetById(request.StoryId);
        if (story == null)
            throw new InvalidOperationException("Truyện không tồn tại.");
        if (story.author_id != authorUserId)
            throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được sử dụng tính năng đồng sáng tác.");
        //lấy toàn bộ chương và xắp xếp theo thứ tự
        var allChaptersOrdered = _chapterRepository.GetByStoryId(request.StoryId).OrderBy(c => c.order_index).ToList();
        //xác định chương đang sử dụng AI
        var targetOrderForWarning = ResolveCoCreateTargetOrderIndex(request, allChaptersOrdered);
        var contextWarning = ChapterAiContextWarningHelper.GetWarningIfApplicable(allChaptersOrdered, targetOrderForWarning);
        //kiểm tra có ý tưởng của tác giả hay không,nếu có thì dùng ý tưởng đó, nếu không có thì dùng câu hướng dẫn chung cho AI viết tiếp theo mạch truyện hiện có
        var hasAuthorIdea = !string.IsNullOrWhiteSpace(rawIdea);
        var effectiveIdea = hasAuthorIdea
            ? rawIdea!
            : "Hãy viết tiếp chương tiếp theo một cách tự nhiên dựa trên mạch truyện hiện có (không thêm plot twist lớn nếu chưa được gợi mở).";

        //tạo câu truy vấn để lấy context phục vụ RAG: nếu có ý tác giả thì ưu tiên lấy ý đó làm truy vấn; nếu không có thì tạo câu truy vấn từ nội dung chương đã xuất bản gần nhất để RAG bám sát mạch truyện hiện tại.
        var ragQueryForRetrieval = hasAuthorIdea
            ? rawIdea!
            : CoCreateRagQueryFromLatestPublishedChapter(request.StoryId, story.title);

        var publishedWithContent = _chapterRepository
            .GetPublishedByStoryId(request.StoryId)
            .Where(c => !string.IsNullOrWhiteSpace(c.content))
            .ToList();
        if (publishedWithContent.Count == 0)
            throw new InvalidOperationException(
                "Truyện cần có ít nhất một chương đã xuất bản (PUBLISHED) và có nội dung để đồng sáng tác.");

        var ragStatus = _storyRagService.GetRagStatus(request.StoryId);
        if (!ragStatus.EmbeddingConfigured)
        {
            throw new InvalidOperationException(
                "Chưa cấu hình embedding: cần AI:EmbeddingBaseUrl (vd. https://openrouter.ai/api/v1), AI:EmbeddingModel (vd. openai/text-embedding-3-small) và API key AI:ApiKey hoặc AI:EmbeddingApiKey. Với OpenRouter thường dùng cùng key với chat; đặt trong appsettings.Local.json hoặc biến môi trường trên server.");
        }

        await _storyRagService.TryEnsureIndexedAsync(request.StoryId, request.ChapterId, cancellationToken);
        if (!_storyRagService.IsRagAvailableForStory(request.StoryId))
        {
            throw new InvalidOperationException(
                "Truyện chưa có chỉ mục RAG (chưa có chunk vector). Đảm bảo chương PUBLISHED có nội dung sau khi chunk, rồi gọi POST /api/ai/index-rag hoặc thử lại — kiểm tra thư mục VectorStore (Data/faiss) có quyền ghi trên server.");
        }

        //xây dựng ngữ cảnh cho cả Agent 1 và Agent 2 (cùng dùng chung): Story State, Event Memory, Character Memory, RAG. Việc xây dựng này có thể tốn thời gian nếu truyện có nhiều chương và nhiều nhân vật
        string contextBlock = await _memoryEngine.BuildContextForCoCreateAsync(
            request.StoryId, effectiveIdea, ragQueryForRetrieval, cancellationToken);

        var languageInstruction = StoryLanguageHelper.VietnameseOnlyInstruction;

        var durations = new List<AgentDuration>();

        // --- Agent 1 (Story Analyzer / Planner): Dàn ý theo kiến trúc Prompt + RAG + Memory + Agent Role ---
        var (p1, m1, k1, u1) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlanner);
        var clientPlanner = AIClientHelper.CreateChatClient(p1, m1, k1, u1);
        var swOutline = Stopwatch.StartNew();
        var (outlineJson, outlineCompletion) = await RunAgent1OutlineAsync(
            clientPlanner, story, contextBlock, effectiveIdea, languageInstruction, cancellationToken, prioritizeAuthorIdea: hasAuthorIdea);
        swOutline.Stop();
        durations.Add(new AgentDuration { Step = "Outline", DurationMs = swOutline.ElapsedMilliseconds });
        LogUsageFromCompletion(authorUserId, request.StoryId, null, ActionOutline, m1, outlineCompletion);

        if (!IsAgent1OutlineJsonAcceptable(outlineJson))
        {
            swOutline = Stopwatch.StartNew();
            (outlineJson, outlineCompletion) = await RunAgent1OutlineRetryAsync(
                clientPlanner, story, contextBlock, effectiveIdea, languageInstruction, cancellationToken, prioritizeAuthorIdea: hasAuthorIdea, invalidResponse: outlineJson);
            swOutline.Stop();
            durations.Add(new AgentDuration { Step = "Outline_Retry", DurationMs = swOutline.ElapsedMilliseconds });
            LogUsageFromCompletion(authorUserId, request.StoryId, null, ActionOutline, m1, outlineCompletion);
        }

        //tách tiêu đề gợi ý+thân dàn ý+danh sách nhân vật liên quan từ JSON trả về của Agent 1. Nếu JSON không hợp lệ hoặc thiếu trường, sẽ có fallback để tránh lỗi.
        var (suggestedTitle, outlineBody, charactersFromPayload) = TryExtractOutlineAndSuggestedTitle(outlineJson);
        //đảm bảo tiêu đề kh bị trùng với tiêu đề chương đã có (nếu Agent 1 sinh ra tiêu đề trùng thì sẽ tự động thêm hậu tố số để phân biệt)
        suggestedTitle = EnsureUniqueSuggestedTitle(suggestedTitle, allChaptersOrdered);
        //chuẩn hóa dàn ý để đưa vào promt trước khi viết
        var outlineForPrompt = FormatOutlineForPrompt(outlineBody);
        //nếu payload có danh sách nvat thì đùng luôn nếu kh có thì phân tích từ dàn ý để lấy danh sách nhân vật liên quan (cách này có thể không chính xác bằng việc Agent 1 liệt kê nhưng sẽ đảm bảo có danh sách để dùng cho Agent 2 và tránh lỗi nếu Agent 1 không trả về đúng format)
        var charactersInvolved = (charactersFromPayload?.Count ?? 0) > 0
            ? charactersFromPayload!
            : ExtractCharactersInvolved(outlineForPrompt);
        //Agent2 
        var (p2, m2, k2, u2) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentWriter);
        var clientWriter = AIClientHelper.CreateChatClient(p2, m2, k2, u2);

        var swWrite = Stopwatch.StartNew();
        //gọi Agent 2 để viết bản nháp đầu tiên dựa trên dàn ý và ngữ cảnh đã xây dựng
        var (draft, writeCompletion) = await RunAgent2WriteAsync(clientWriter, contextBlock, outlineForPrompt, languageInstruction, suggestedTitle, cancellationToken);
        swWrite.Stop();
        //lưu bản nháp gốc trước khi sửa để có thể so sánh và log số từ đã bị cắt nếu có phần feedback bị loại bỏ ở bước sau. Việc này cũng giúp log chính xác hơn về số từ mà Agent 2 đã viết ra ban đầu, thay vì số từ sau khi đã cắt bỏ phần feedback (nếu có).
        var rawDraft = draft;
        //đếm số từ trong bản nháp gốc
        var rawWordCount = CountWords(rawDraft);
        //cắt phần phản hồi (feedback) nếu Agent 2 có thêm vào cuối bản nháp (đôi khi Agent 2 có thể thêm phần feedback cho tác giả ở cuối bản nháp, đặc biệt khi có yêu cầu sửa từ cấm hoặc khi có hướng dẫn sửa trong prompt). Việc cắt bỏ này giúp đảm bảo rằng phần feedback không bị đếm vào số từ của bản nháp chính và không bị lưu vào database nếu chỉ muốn lưu nội dung truyện.
        draft = StripTrailingFeedbackFromDraft(rawDraft);
        //đếm lại số từ sau khi cắt
        var strippedWordCount = CountWords(draft);

        //không liên quan
        var feedbackMarkerIndex = rawDraft.IndexOf("\n\nFeedback:", StringComparison.OrdinalIgnoreCase);
        if (feedbackMarkerIndex < 0)
            feedbackMarkerIndex = rawDraft.IndexOf("\nFeedback:", StringComparison.OrdinalIgnoreCase);
        var feedbackTrimmed = feedbackMarkerIndex >= 0;

        //lấy token usage từ completion của Agent 2 để log (cả prompt và completion tokens, cũng như tổng tokens) — thông tin này rất hữu ích để theo dõi và tối ưu chi phí AI, đặc biệt khi có nhiều lần sửa do từ cấm hoặc khi mở rộng độ dài.(debug thôi)
        var (writePromptTokens, writeCompletionTokens, writeTotalTokens) = AiChatCompletionUsageHelper.GetTokenCounts(writeCompletion);
        durations.Add(new AgentDuration { Step = "Write", DurationMs = swWrite.ElapsedMilliseconds });
        _logger.LogInformation(
            "CoCreate Write diagnostics StoryId={StoryId} RawWords={RawWords} FinalWords={FinalWords} FeedbackTrimmed={FeedbackTrimmed} PromptTokens={PromptTokens} CompletionTokens={CompletionTokens} TotalTokens={TotalTokens} DurationMs={DurationMs}",
            request.StoryId,
            rawWordCount,
            strippedWordCount,
            feedbackTrimmed,
            writePromptTokens,
            writeCompletionTokens,
            writeTotalTokens,
            swWrite.ElapsedMilliseconds);

        //ghi log token/cost cho bước viết bản nháp đầu tiên của Agent 2
        LogUsageFromCompletion(authorUserId, request.StoryId, null, ActionWrite, m2, writeCompletion);
        //check từ cấm 
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
            durations,
            phaseLabel: "");
        //cập nhập lại draft sau khi đã sửa xong (có thể sửa nhiều lần nếu bật tự sửa và vẫn còn từ cấm hoặc lỗi chính tả sau lần sửa đầu tiên)
        draft = draftAfterRefine;

        // nếu draft ngắn hơn mức tối thiểu thì gọi Agent 2 mở rộng độ dài.
        if (CountWords(draft) < MinDraftWordCount)
        {
            var swExpand = Stopwatch.StartNew();
            var (expandedDraft, expandCompletion) = await RunAgent2ExpandAsync(
                clientWriter,
                contextBlock,
                outlineForPrompt,
                draft,
                languageInstruction,
                suggestedTitle,
                cancellationToken);
            //nếu phần mở rộng rỗng thì giữ nguyên,không rỗng thì nối tiếp vào bản nháp hiện tại
            draft = string.IsNullOrWhiteSpace(expandedDraft)
                ? draft
                : $"{draft.TrimEnd()}\n\n{expandedDraft.TrimStart()}";
            swExpand.Stop();
            draft = StripTrailingFeedbackFromDraft(draft);
            durations.Add(new AgentDuration { Step = "Length_Expand", DurationMs = swExpand.ElapsedMilliseconds });
            //ghi log token/cost cho bước mở rộng độ dài
            LogUsageFromCompletion(authorUserId, request.StoryId, null, ActionWrite, m2, expandCompletion);
            //chạy lại check từ cấm sau khi thêm nội dung mở rộng, nếu vẫn còn từ cấm hoặc lỗi chính tả thì tiếp tục sửa (có thể sửa nhiều lần nếu vẫn còn vấn đề sau lần sửa đầu tiên)
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
                durations,
                phaseLabel: "Length_Expand_");
            //cập nhập draf mới
            draft = draftExpanded;
            revisionCount += expandRewrites;
            if (!expandApproved)
            {
                approved = false;
                reviewFeedback = expandReviewFeedback;
            }
        }
        //kiểm tra độ dài cuối cùng, nếu vẫn ngắn hơn mức tối thiểu thì trả về lỗi yêu cầu thử lại (có thể do Agent 2 không mở rộng được hoặc phần mở rộng vẫn còn bị cắt ngắn do có chứa từ cấm hoặc lỗi chính tả nên bị loại bỏ trong phần feedback)
        var finalWordCount = CountWords(draft);
        if (finalWordCount < MinDraftWordCount)
            throw new InvalidOperationException($"Nội dung AI tạo ra quá ngắn ({finalWordCount} từ), yêu cầu phải lớn hơn 500 từ. Vui lòng thử lại.");

        //lưu vào bảng AI Generated Content 
        var saved = SaveAiGeneratedContentOnly(
            request.StoryId,
            authorUserId,
            hasAuthorIdea ? rawIdea! : "[AUTO] Tiếp tục theo mạch truyện (không có gợi ý tác giả)",
            draft,
            request.ChapterOrderIndex,
            request.ChapterId);
        //trả response cuối cùng
        return new CoCreationResponse
        {
            Outline = outlineForPrompt,
            SuggestedChapterTitle = suggestedTitle,
            CharactersInvolved = charactersInvolved,
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

    /// <summary>Khi tác giả không nhập gợi ý: nội dung chương publish mới nhất làm query RAG; nếu vượt ngưỡng embedding thì chỉ lấy đoạn cuối (cùng AI:EmbeddingQueryMaxChars với StoryRagService).</summary>
    private string CoCreateRagQueryFromLatestPublishedChapter(Guid storyId, string? storyTitleFallback)
    {
        var latestPublished = _chapterRepository
            .GetPublishedByStoryId(storyId)
            .Where(c => !string.IsNullOrWhiteSpace(c.content))
            .OrderByDescending(c => c.order_index)
            .FirstOrDefault();
        var latestContent = latestPublished?.content?.Trim();
        if (!string.IsNullOrWhiteSpace(latestContent))
        {
            var maxChars = _configuration.GetValue("AI:EmbeddingQueryMaxChars", DefaultEmbeddingRagQueryMaxChars);
            if (maxChars < 256)
                maxChars = DefaultEmbeddingRagQueryMaxChars;
            if (latestContent.Length > maxChars)
                return latestContent[^maxChars..];
            return latestContent;
        }
        var title = storyTitleFallback?.Trim();
        return string.IsNullOrWhiteSpace(title) ? string.Empty : title;
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

Ưu tiên theo User Idea (trong prompt user):
- Nếu User Idea là ý diễn biến CỤ THỂ do tác giả nhập: dàn ý PHẢI bám sát và thực hiện hướng đó làm trọng tâm; dùng context (Story State, Event, Character, RAG) để tiếp nối SAU điểm kết thúc hiện tại, đúng tên nhân vật/địa danh, không viết lại quá khứ như scene mới.
- Nếu User Idea chỉ là hướng dẫn chung (không có plot cụ thể): dàn ý tiếp nối mạch truyện; tham khảo thứ tự Story State → Event Memory → Character Memory → RAG khi cần.

---

Constraints:
- CHỈ viết những gì xảy ra TIẾP THEO, không lặp lại hoặc mô tả lại sự kiện đã xảy ra.
- Không phá vỡ timeline “điểm kết thúc hiện tại → tiếp theo” (chỉ mô tả phần sau mốc đó).
- Không thay đổi tính cách nhân vật nếu không có lý do hợp lý từ context hoặc từ User Idea.
- Không tự ý thêm yếu tố lớn (sức mạnh mới, phe phái mới, plot twist lớn) nếu chưa được chuẩn bị trong context — trừ khi User Idea yêu cầu rõ hướng đó.
- Sử dụng chính xác tên nhân vật như trong context (không dịch, không thay đổi).
- Đảm bảo diễn tiến hợp lý từ điểm kết thúc hiện tại.
- Giữ đúng tone của truyện (dark, romance, comedy,...).

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
- Viết hoàn toàn bằng tiếng Việt; không trộn ngôn ngữ khác.

---

Output Format (bắt buộc):
→ Trả về DUY NHẤT một JSON hợp lệ, không markdown, không ký tự ngoài JSON.
→ Không ghi chú kiểu "Dàn ý" hay văn bản trước dấu { ; bắt đầu ngay bằng {.
→ Trong chuỗi JSON, không được có xuống dòng thật bên trong dấu ngoặc kép; mọi xuống dòng trong outline phải là hai ký tự \\n (escape) trong chuỗi.
Cấu trúc:
{
  "suggestedChapterTitle": "Một dòng tiêu đề gợi ý (ngắn, gợi tình tiết; không prefix kiểu \"Chương 1\" hay \"Chapter 1\")",
  "outline": "Toàn bộ dàn ý: Scene Objective, Scene Outline (2–7 ý), Characters Involved, Potential Conflict, Expected Outcome — dùng \\n trong chuỗi JSON để xuống dòng giữa các mục.",
  "charactersInvolved": ["Tên nhân vật 1", "Tên nhân vật 2"]
}
Trường suggestedChapterTitle bắt buộc (chuỗi, có thể rỗng "" nếu không đặt được tiêu đề ngắn).
Trường outline bắt buộc, nội dung đúng cấu trúc Scene như các mục Scene Objective / Scene Outline / … đã nêu phía trên.
Trường charactersInvolved bắt buộc: mảng tên nhân vật (không thêm mô tả như "để tạo...", "hook", v.v.).

""" + "\n\n" + ConstitutionalRules;

    private static string GetAgent2CorrectSystemPrompt() => """
Role:
Bạn là AI viết truyện (Story Writer) — lần này nhiệm vụ là SỬA bản nháp đã có theo yêu cầu cụ thể.

Quy tắc:
- Thực hiện đầy đủ các sửa được liệt kê (từ cấm → thay bằng diễn đạt tương đương).
- Không đổi cốt truyện, timeline, nhân vật so với bản nháp và dàn ý; không thêm plot twist lớn.
- Giữ tiếng Việt như bản gốc; độ dài không được ngắn hơn đáng kể so với bản nháp (nếu bản đã ≥500 từ thì bản sau sửa cũng ≥500 từ).
- Chỉ trả về toàn bộ văn bản sau sửa, không markdown, không giải thích.
""" + "\n\n" + ConstitutionalRules;

    private static string? BuildCorrectionInstructionBlock(GuardrailResult gr)
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
        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    private async Task<(string Text, ChatCompletion Completion)> RunAgent2CorrectAsync(
        ChatClient client,
        string contextBlock,
        string outline,
        string currentDraft,
        string languageInstruction,
        string correctionBlock,
        CancellationToken ct)
    {
        var userPrompt =
            $"{contextBlock}\n\n---\n{Agent2Checklist}\n---\nDàn ý (không đổi hướng plot):\n{outline}\n\n" +
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
        var c = completion.Value;
        var text = c.Content?.Count > 0 ? c.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent sửa bản nháp không trả về nội dung.");
        return (text.Trim(), c);
    }

    /// <summary>Lặp tối đa N lần: kiểm tra từ cấm → nếu lỗi thì Agent 2 viết lại.</summary>
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

            if (gr.Passed)
            {
                return (draft, true, null, rewrites);
            }

            var instr = BuildCorrectionInstructionBlock(gr);
            if (string.IsNullOrWhiteSpace(instr))
                break;

            var swW = Stopwatch.StartNew();
            var (correctedDraft, correctCompletion) = await RunAgent2CorrectAsync(clientWriter, contextBlock, outlineForPrompt, draft, languageInstruction, instr, ct);
            draft = correctedDraft;
            swW.Stop();
            draft = StripTrailingFeedbackFromDraft(draft);
            rewrites++;
            durations.Add(new AgentDuration { Step = $"{prefix}_Rewrite", DurationMs = swW.ElapsedMilliseconds });
            LogUsageFromCompletion(authorUserId, storyId, null, ActionWriteCorrect, writerModelName, correctCompletion);
        }

        var swGf = Stopwatch.StartNew();
        var grFinal = await _guardrail.CheckAsync(storyId, draft, ct);
        swGf.Stop();
        durations.Add(new AgentDuration { Step = $"{phaseLabel}SelfCorrect_Final_Banned", DurationMs = swGf.ElapsedMilliseconds });

        var okFinal = grFinal.Passed;
        string? review = null;
        if (!okFinal)
        {
            var bits = new List<string>();
            if (!grFinal.Passed)
                bits.Add(string.Join(" ", grFinal.Violations.Select(v => $"[{v.Type}] {v.Message}")));
            review = string.Join(" ", bits);
        }

        return (draft, okFinal, review, rewrites);
    }

    private static string BuildAgent1OutlineUserPrompt(
        stories story,
        string contextBlock,
        string authorIdea,
        string languageInstruction,
        bool prioritizeAuthorIdea)
    {
        var storyInfo = $"Story Information:\nTitle: {story.title}\n\nUser Idea:\n{authorIdea}";
        var directionNote = prioritizeAuthorIdea
            ? "User Idea là hướng sáng tác do tác giả đặt: dàn ý PHẢI thực hiện ý đó làm trọng tâm cho phần TIẾP THEO; dùng ngữ cảnh DB để tiếp nối SAU điểm kết thúc hiện tại và giữ đúng tên nhân vật, địa danh."
            : "Không có ý plot cụ thể từ tác giả (chỉ hướng dẫn chung): dàn ý tiếp nối tự nhiên theo mạch truyện, Story State, Event Memory và Character Memory.";
        return $"{storyInfo}\n\n---\n{contextBlock}\n\n---\n{languageInstruction}\n\n{directionNote}\n\nNgữ cảnh trên là phần truyện ĐÃ XẢY RA. Chỉ sinh outline cho phần TIẾP THEO (sau điểm kết thúc hiện tại). Trả lời bằng tiếng Việt. LUÔN trả về đúng một JSON có suggestedChapterTitle + outline + charactersInvolved theo system prompt (không trả JSON khác, không trả nhánh từ chối).";
    }

    private async Task<(string Text, ChatCompletion Completion)> RunAgent1OutlineAsync(
        ChatClient client,
        stories story,
        string contextBlock,
        string authorIdea,
        string languageInstruction,
        CancellationToken ct,
        bool prioritizeAuthorIdea)
    {
        var userPrompt = BuildAgent1OutlineUserPrompt(story, contextBlock, authorIdea, languageInstruction, prioritizeAuthorIdea);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent1SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentPlanner);
        var completion = await client.CompleteChatAsync(messages, options);
        var c = completion.Value;
        var text = c.Content?.Count > 0 ? c.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent dàn ý không trả về nội dung.");
        return (text.Trim(), c);
    }

    private async Task<(string Text, ChatCompletion Completion)> RunAgent1OutlineRetryAsync(
        ChatClient client,
        stories story,
        string contextBlock,
        string authorIdea,
        string languageInstruction,
        CancellationToken ct,
        bool prioritizeAuthorIdea,
        string invalidResponse)
    {
        var userPrompt = BuildAgent1OutlineUserPrompt(story, contextBlock, authorIdea, languageInstruction, prioritizeAuthorIdea);
        const int maxSnippetChars = 12000;
        var fullInvalid = invalidResponse.Trim();
        var snippet = fullInvalid;
        if (snippet.Length > maxSnippetChars)
            snippet = snippet[^maxSnippetChars..];
        var fixInstruction =
            "Bản phản hồi trước KHÔNG phải JSON hợp lệ hoặc thiếu/không đúng kiểu các trường suggestedChapterTitle, outline (chuỗi), charactersInvolved (mảng). " +
            "Trả về DUY NHẤT một JSON hợp lệ: bắt đầu bằng { và kết bằng }; trong giá trị outline mọi xuống dòng phải là \\\\n trong chuỗi JSON, không được xuống dòng thật bên trong dấu ngoặc kép. " +
            "Không markdown, không chữ \"Dàn ý\" hay văn bản trước JSON.\n\nBản cần sửa thành JSON đúng:\n---\n" +
            snippet +
            "\n---";
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent1SystemPrompt()),
            new UserChatMessage(userPrompt),
            new AssistantChatMessage(fullInvalid),
            new UserChatMessage(fixInstruction)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentPlanner);
        var completion = await client.CompleteChatAsync(messages, options);
        var c = completion.Value;
        var text = c.Content?.Count > 0 ? c.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent dàn ý (retry) không trả về nội dung.");
        return (text.Trim(), c);
    }

    private async Task<(string Text, ChatCompletion Completion)> RunAgent2ExpandAsync(
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
        var draftTail = ExtractTailChars(currentDraft, 2200);
        var userPrompt =
            $"{Agent2Checklist}\n\nDàn ý:\n{outline}\n\nPhần cuối bản nháp hiện tại (để nối mạch, KHÔNG viết lại đoạn cũ):\n{draftTail}{titleHint}\n\n" +
            $"{languageInstruction}\n\n" +
            $"Yêu cầu: bổ sung PHẦN NỐI TIẾP NGẮN để đưa tổng độ dài bản nháp lên khoảng {ExpandTargetWords} từ (tối thiểu >500). " +
            "Chỉ viết phần mới nối tiếp, không viết lại nội dung cũ, không đổi hướng plot, không thêm plot twist lớn. " +
            "Trả về DUY NHẤT phần nội dung bổ sung mới (không lặp lại bản nháp cũ).";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent2SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentWriter);
        var completion = await client.CompleteChatAsync(messages, options);
        var c = completion.Value;
        var text = c.Content?.Count > 0 ? c.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent mở rộng nội dung không trả về nội dung.");
        return (text.Trim(), c);
    }

    private static string ExtractTailChars(string text, int maxChars)
    {
        var s = (text ?? string.Empty).Trim();
        if (s.Length <= maxChars) return s;
        return s[^maxChars..];
    }

    private static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        return text
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Length;
    }

    private static bool IsAgent1OutlineJsonAcceptable(string outlineRaw)
    {
        var raw = outlineRaw.Trim();
        if (raw.Length == 0)
            return false;
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
                return false;
            if (!root.TryGetProperty("outline", out var outlineEl) || outlineEl.ValueKind != JsonValueKind.String)
                return false;
            return !string.IsNullOrWhiteSpace(outlineEl.GetString());
        }
        catch
        {
            return false;
        }
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
        try
        {
            var root = JsonDocument.Parse(raw).RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return outlineRaw;

            if (root.TryGetProperty("scenes", out var scenes) && scenes.ValueKind == JsonValueKind.Array && scenes.GetArrayLength() > 0)
            {
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

            if (root.TryGetProperty("outline", out var outlineEl) && outlineEl.ValueKind == JsonValueKind.String)
            {
                var outlineText = NormalizeOutlineText(outlineEl.GetString());
                var blocks = new List<string>();
                if (root.TryGetProperty("suggestedChapterTitle", out var st) && st.ValueKind == JsonValueKind.String)
                {
                    var tit = NormalizeSuggestedTitle(st.GetString());
                    if (!string.IsNullOrEmpty(tit))
                        blocks.Add($"Tiêu đề gợi ý: {tit}");
                }
                else if (root.TryGetProperty("suggested_chapter_title", out var st2) && st2.ValueKind == JsonValueKind.String)
                {
                    var tit = NormalizeSuggestedTitle(st2.GetString());
                    if (!string.IsNullOrEmpty(tit))
                        blocks.Add($"Tiêu đề gợi ý: {tit}");
                }
                if (!string.IsNullOrWhiteSpace(outlineText))
                    blocks.Add(outlineText);
                var fromRoot = TryReadCharactersFromRoot(root);
                if (fromRoot is { Count: > 0 })
                    blocks.Add($"Characters Involved:\n- {string.Join("\n- ", fromRoot)}");
                if (blocks.Count > 0)
                    return string.Join("\n\n", blocks);
            }
        }
        catch
        {
            return outlineRaw;
        }

        return outlineRaw;
    }

    /// <summary>Checklist ràng buộc cho Agent 2: đối chiếu ngữ cảnh trước khi viết.</summary>
    private const string Agent2Checklist = """
Trước khi viết, hãy tự kiểm tra dựa trên “Ngữ cảnh từ database” phía trên:
(1) Ngữ cảnh mô tả những gì ĐÃ xảy ra; bạn chỉ được viết phần TIẾP THEO (các sự kiện xảy ra SAU điểm kết thúc hiện tại). Tuyệt đối không viết lại quá khứ như scene đang diễn ra hiện tại (ví dụ: đã tang lễ thì không viết cảnh hấp hối như đang xảy ra).
(2) Ưu tiên thực hiện đúng Scene Outline và phần «Ý tưởng tác giả» trong ngữ cảnh (nếu có ý cụ thể); giữ đúng tên nhân vật/địa danh như trong context.
(3) Nếu outline mô tả diễn biến đặc biệt (vd. nhân vật trở lại, twist), hãy viết theo outline; chỉ cần không mâu thuẫn với chính timeline “điểm kết thúc hiện tại → tiếp theo”.
(4) Bám đúng thứ tự và nội dung của Scene Outline.
Nếu outline còn mơ hồ, suy luận theo hướng bám outline và mạch truyện đã có.
""";

    private static string GetAgent2SystemPrompt() => """
Role:
Bạn là AI viết truyện (Story Writer), viết chương TIẾP THEO dựa trên ngữ cảnh DB và Scene Outline.

Nguyên tắc cốt lõi:
1) Chỉ viết các sự kiện xảy ra SAU điểm kết thúc hiện tại; không viết lại quá khứ như scene đang diễn ra.
2) Ưu tiên theo thứ tự: Scene Outline -> Ý tưởng tác giả (nếu có) -> Story State -> Event Memory -> Character Memory -> RAG.
3) Giữ chính xác tên nhân vật/địa danh/thuật ngữ trong ngữ cảnh.
4) Viết đúng thứ tự các ý outline, không bỏ sót ý chính; được phép mở rộng chi tiết nhưng không đổi ý nghĩa outline.
5) Không tự ý thêm nhân vật quan trọng mới, sức mạnh mới, plot twist lớn nếu chưa có trong ngữ cảnh/outline.

Chất lượng văn bản:
- Văn phong tự nhiên, cảm xúc, ưu tiên show-don't-tell.
- Kết hợp hành động, đối thoại, nội tâm và chuyển cảnh hợp lý.
- Tránh lặp thông tin; giữ nhịp truyện tốt, không kết thúc sớm.

Ngôn ngữ:
- Chỉ dùng tiếng Việt.

Output:
- Chỉ trả về nội dung truyện hoàn chỉnh.
- Không markdown, không tiêu đề, không giải thích, không meta text.
""" + "\n\n" + ConstitutionalRules;

    private async Task<(string Text, ChatCompletion Completion)> RunAgent2WriteAsync(
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
        var userPrompt = $"{contextBlock}\n\n---\n{Agent2Checklist}\n---\nDàn ý cần viết:\n{outline}{titleHint}";
        userPrompt += $"\n\n{languageInstruction}\n\nĐộ dài bắt buộc: mục tiêu 700-850 từ. Trước khi trả lời, tự kiểm tra ước lượng số từ lần cuối; nếu chưa đạt thì tiếp tục mở rộng chi tiết hợp lý (đối thoại, nội tâm, chuyển cảnh) mà không đổi mạch truyện. Chỉ output nội dung chương (văn bản truyện), không markdown hay giải thích.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetAgent2SystemPrompt()),
            new UserChatMessage(userPrompt)
        };
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentWriter);
        var completion = await client.CompleteChatAsync(messages, options);
        var c = completion.Value;
        var text = c.Content?.Count > 0 ? c.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            throw new InvalidOperationException("Agent viết nội dung không trả về nội dung.");
        return (text.Trim(), c);
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

    private void LogUsageFromCompletion(Guid userId, Guid storyId, Guid? chapterId, string actionType, string modelName, ChatCompletion completion)
    {
        var (promptTokens, completionTokens, totalTokens) = AiChatCompletionUsageHelper.GetTokenCounts(completion);
        _aiUsageLogRepository.Log(new ai_usage_logs
        {
            user_id = userId,
            story_id = storyId,
            chapter_id = chapterId,
            action_type = actionType,
            model_name = modelName,
            generation_id = AiChatCompletionUsageHelper.GetGenerationId(completion),
            cost_usd = AiChatCompletionUsageHelper.TryGetOpenRouterCostUsd(completion),
            prompt_tokens = promptTokens,
            completion_tokens = completionTokens,
            total_tokens = totalTokens,
            status = "SUCCESS",
            created_at = DateTime.UtcNow
        });

        // Debit token balance (clamp >= 0).
        try { UserDAO.DebitAiTokenLimit(userId, totalTokens); } catch { /* best-effort */ }
    }
}
