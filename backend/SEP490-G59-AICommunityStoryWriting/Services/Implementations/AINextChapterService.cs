using System.Text;
using System.Text.Json;
using System.ClientModel;
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

namespace Services.Implementations
{
    public class AINextChapterService : IAINextChapterService
    {
        private const string ActionType = "SUGGEST_NEXT_CHAPTER";
        private const int JsonRepairMaxInputChars = 8000;
        private const string SuggestConstitutionalRules = """
Quy tắc bắt buộc:
- Chỉ dùng dữ liệu ngữ cảnh đã cung cấp (Story Information, RAG, Character Memory, Event Memory, Story State).
- Chỉ gợi ý sự kiện xảy ra SAU điểm kết thúc hiện tại; không lặp lại sự kiện đã xảy ra như nội dung chính.
- Không tạo chi tiết mâu thuẫn với Story State, Event Memory, Character Memory.
- Dùng chính xác tên nhân vật/địa danh/thuật ngữ trong ngữ cảnh; không dịch hay biến đổi tên riêng.
- Hạn chế thêm yếu tố lớn (nhân vật quan trọng mới, sức mạnh mới, phe phái mới, plot twist lớn) nếu ngữ cảnh chưa có gợi mở.
- Tuân thủ đúng output JSON theo format yêu cầu; không thêm markdown hoặc giải thích ngoài JSON.
""";

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IStoryRagService _ragService;
        private readonly IStoryMemoryEngine _memoryEngine;
        private readonly IAIUsageLogRepository _aiUsageLogRepository;
        private readonly IConfiguration _configuration;
        private readonly IUserLookup _userLookup;
        private readonly IAuthorAiTokenBudgetService _authorAiTokenBudget;
        private readonly ILogger<AINextChapterService> _logger;

        public AINextChapterService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IStoryRagService ragService,
            IStoryMemoryEngine memoryEngine,
            IAIUsageLogRepository aiUsageLogRepository,
            IConfiguration configuration,
            IUserLookup userLookup,
            IAuthorAiTokenBudgetService authorAiTokenBudget,
            ILogger<AINextChapterService> logger)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _ragService = ragService;
            _memoryEngine = memoryEngine;
            _aiUsageLogRepository = aiUsageLogRepository;
            _configuration = configuration;
            _userLookup = userLookup;
            _authorAiTokenBudget = authorAiTokenBudget;
            _logger = logger;
        }

        public async Task<SuggestNextChapterResponse> SuggestNextChapterAsync(
            SuggestNextChapterRequest request,
            Guid authorUserId,
            CancellationToken cancellationToken = default)
        {
            if (request.StoryId == Guid.Empty)
                throw new ArgumentException("StoryId là bắt buộc.");

            if (authorUserId == Guid.Empty)
                throw new UnauthorizedAccessException("Không xác định được người dùng. Vui lòng đăng nhập lại.");

            try
            {
                await _authorAiTokenBudget.EnsureWithinBudgetAsync(authorUserId, cancellationToken).ConfigureAwait(false);
            }
            catch (AuthorAiTokenBudgetExceededException ex)
            {
                _logger.LogWarning(ex, "Đã vượt hạn mức token. AuthorUserId={AuthorUserId} StoryId={StoryId}", authorUserId, request.StoryId);
                throw;
            }

            var useHistoryMin = _configuration.GetValue("AI:UseHistoryBasedMinRequiredTokens", true);
            var historyBuffer = _configuration.GetValue("AI:MinRequiredTokensHistoryBuffer", 3000);
            var suggestFallbackMin = _configuration.GetValue("AI:SuggestMinRequiredTokens", 3800);
            var historyMaxSuggest = useHistoryMin
                ? _aiUsageLogRepository.GetMaxTotalTokensForStoryAndActionType(request.StoryId, ActionType)
                : null;
            var minSuggestTokens = AiMinRequiredTokensResolver.ResolveMinRequiredTokens(
                useHistoryMin, historyMaxSuggest, suggestFallbackMin, historyBuffer);
            if (minSuggestTokens > 0)
            {
                var budgetDto = await _authorAiTokenBudget.GetBudgetAsync(authorUserId, cancellationToken).ConfigureAwait(false);
                var remaining = budgetDto?.TokensRemaining;
                if ((remaining ?? long.MaxValue) < minSuggestTokens)
                {
                    var ex = new AuthorAiEstimatedTokensInsufficientException(remaining, minSuggestTokens);
                    _logger.LogWarning(
                        "Không đủ hạn mức token tối thiểu. AuthorUserId={AuthorUserId} StoryId={StoryId} TokensRemaining={TokensRemaining} MinRequired={MinRequired}",
                        authorUserId, request.StoryId, remaining, minSuggestTokens);
                    throw ex;
                }
            }

            var story = _storyRepository.GetById(request.StoryId);
            if (story == null)
                throw new InvalidOperationException("Truyện không tồn tại.");

            if (story.author_id != authorUserId)
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được sử dụng tính năng gợi ý chương.");

            if (_userLookup.IsAuthorWritingSuspended(authorUserId))
                throw new InvalidOperationException("Tài khoản đang bị tạm khóa chức năng viết truyện/chương (compliance/admin), không thể dùng gợi ý AI.");
            //lấy toàn bộ chapter theo thứ tự
            var allChaptersOrdered = _chapterRepository.GetByStoryId(request.StoryId).OrderBy(c => c.order_index).ToList();
            //xác định order_index mục tiêu để gợi ý (thường là sau chương mới nhất, hoặc sau chapter được chỉ định trong request), rồi kiểm tra nếu có cảnh báo ngữ cảnh nào cần hiển thị dựa trên order_index đó (vd. nếu gợi ý sau một chapter có sự kiện quan trọng thì có thể cảnh báo "Gợi ý sẽ dựa trên ngữ cảnh sau chapter X, nơi có sự kiện Y. Nếu muốn gợi ý dựa trên ngữ cảnh khác, hãy chọn lại chapter đích.")
            var targetOrderForWarning = ResolveSuggestTargetOrderIndex(request, allChaptersOrdered);
            var contextWarning = ChapterAiContextWarningHelper.GetWarningIfApplicable(allChaptersOrdered, targetOrderForWarning);
            //lấy chapter đã publish 
            var chapters = _chapterRepository.GetPublishedByStoryId(request.StoryId).ToList();
            var hasContent = chapters.Any(c => !string.IsNullOrWhiteSpace(c.content));
            if (!hasContent)
                throw new InvalidOperationException("Truyện cần có ít nhất một chương đã xuất bản (PUBLISHED) và có nội dung để gợi ý chương tiếp theo.");

            var ragStatus = _ragService.GetRagStatus(request.StoryId);
            if (!ragStatus.EmbeddingConfigured)
            {
                throw new InvalidOperationException(
                    "Chưa cấu hình embedding: cần AI:EmbeddingBaseUrl (vd. https://openrouter.ai/api/v1), AI:EmbeddingModel (vd. openai/text-embedding-3-small) và API key AI:ApiKey hoặc AI:EmbeddingApiKey. Với OpenRouter thường dùng cùng key với chat; đặt trong appsettings.Local.json hoặc biến môi trường trên server.");
            }
            //nếu truyện chưa được index thì tiến hành index (chunk + embedding) trước khi retrieve để đảm bảo có ngữ cảnh RAG cho gợi ý. Nếu đã index rồi thì không làm gì.
            await _ragService.TryEnsureIndexedAsync(request.StoryId, upToChapterId: request.UpToChapterId, cancellationToken);
            if (!_ragService.IsRagAvailableForStory(request.StoryId))
            {
                throw new InvalidOperationException(
                    "Truyện chưa có chỉ mục RAG (chưa có chunk vector). Đảm bảo chương PUBLISHED có nội dung sau khi chunk, rồi gọi POST /api/ai/index-rag hoặc thử lại — kiểm tra thư mục VectorStore (Data/faiss) có quyền ghi trên server.");
            }
            //lấy nội dung chương gần nhất
            //dùng nội dung đó để làm truy vấn RAG
            var lastChapterContent = chapters.LastOrDefault()?.content ?? "";
            var ragQuery = lastChapterContent.Trim();
            var contextBlock = await _memoryEngine.BuildContextForSuggestAsync(request.StoryId, ragQuery, cancellationToken);

            var languageInstruction = StoryLanguageHelper.VietnameseOnlyInstruction;
            //lấy cấu hình model chat cho agent planner
            var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlanner);
            //tạo AI từ config đó
            var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);
            //tạo system promt và user promt
            var systemPrompt = GetSystemPrompt();
            var userPrompt = GetUserPrompt(story, contextBlock, languageInstruction);
            //gộp thành danh sách message gửi cho model
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };
            //lấy số token còn lại của user để set max token cho response (cộng thêm reserve cho input tokens), nếu số dư token còn lại quá thấp so với reserved thì throw lỗi yêu cầu nạp thêm token.
            var balanceNow = Math.Max(0L, UserDAO.GetAiTokenLimit(authorUserId));
            // lấy số token dự trữ cho input
            var reservedInputTokens = _configuration.GetValue("AI:SuggestReservedInputTokens", 2600);
            //tính số token tối đa có thể dùng cho output (còn lại sau khi trừ đi reserved input tokens), đảm bảo tối thiểu 64 token cho output để tránh lỗi model trả về quá ít token.
            var cappedOutputTokens = (int)Math.Max(64, balanceNow - reservedInputTokens);
            var options = AIClientHelper.GetCompletionOptions(_configuration, null, cappedOutputTokens);
            //khai báo biến chứa kết quả của completion để ghi log sau khi gọi, và danh sách các completion (cả lần gọi chính và lần retry nếu có) để tổng hợp usage token và cost cho log.
            ChatCompletion completion;
            //tạo list để lưu tất cả completion (cả lần chính và lần retry nếu có) để ghi log usage sau cùng. Nếu lần chính có lỗi thì sẽ không có completion nào, nhưng vẫn ghi log với status "FAILURE" trong catch.
            var completionsForUsage = new List<ChatCompletion>();
            try
            {
                //Gọi modal để sinh gợi ý chương tiếp theo dựa trên ngữ cảnh đã xây dựng. Nếu có lỗi kết nối hoặc lỗi từ provider, catch và throw với thông điệp rõ ràng.
                completion = await client.CompleteChatAsync(messages, options);
                //nếu thnanhf công thì mới add vào list để ghi log usage sau cùng, nếu lỗi thì sẽ không có completion nào và sẽ ghi log với status "FAILURE" trong catch.
                completionsForUsage.Add(completion);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau hoặc kiểm tra cấu hình API key.",
                    ex);
            }
            //lấy text từ kết quả completion,nếu không có text hoặc text rỗng thì throw lỗi. Nếu có text thì parse JSON để lấy danh sách gợi ý. Nếu parse lỗi hoặc không có gợi ý nào thì thử gọi lại với prompt đã được sửa chữa để hướng dẫn model trả về đúng JSON (thường do model trả về thêm markdown hoặc giải thích ngoài JSON nên parse lỗi).
            var text = completion.Content?.Count > 0 ? completion.Content[0].Text : null;
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("AI không trả về nội dung gợi ý.");
            //parse JSON để lấy danh sách gợi ý
            var suggestions = ParseSuggestions(text);
            //nếu parse xong mà không có gợi ý nào thì thử gọi lại với prompt đã được sửa chữa để hướng dẫn model trả về đúng JSON (thường do model trả về thêm markdown hoặc giải thích ngoài JSON nên parse lỗi). Nếu vẫn không có gợi ý nào sau lần sửa chữa thì throw lỗi.
            if (suggestions.Count == 0)
            {
                var repaired = await RetryNormalizeSuggestionsJsonAsync(
                    client,
                    options,
                    systemPrompt,
                    text,
                    cancellationToken).ConfigureAwait(false);
                if (repaired.Completion != null)
                    completionsForUsage.Add(repaired.Completion);
                suggestions = repaired.Suggestions;
                if (suggestions.Count == 0)
                {
                    var snippet = repaired.Text.Length > 600 ? repaired.Text[..600] + "..." : repaired.Text;
                    throw new InvalidOperationException(
                        "Không thể đọc được gợi ý từ phản hồi AI. Kiểm tra model trả về đúng JSON với mảng \"suggestions\" (title, summary, direction, key_events, characters_involved). Phản hồi AI (rút gọn): " + snippet);
                }
            }
            //loại trùng tiêu đề với chapter cũ
            var existingTitles = allChaptersOrdered
                .Select(c => c.title?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dtoList = BuildUniqueSuggestionDtos(suggestions, existingTitles);
            //lấy chapter id để ghi log usage: ưu tiên chapter id từ request.ChapterId (chương đang soạn), nếu không có thì lấy chapter id của chương đã publish gần nhất (chapters đã được filter chỉ lấy chương published ở trên).
            Guid? usageLogChapterId = chapters.LastOrDefault()?.id;
            if (request.ChapterId.HasValue)
            {
                var targetChapter = _chapterRepository.GetById(request.ChapterId.Value);
                if (targetChapter != null && targetChapter.story_id != request.StoryId)
                    throw new InvalidOperationException("ChapterId không khớp truyện.");
                if (targetChapter != null)
                    usageLogChapterId = targetChapter.id;
            }
            //Ghi token  
            //cộng tổng input/output token từ tất cả completion (lần chính và lần retry nếu có) để ghi log usage. Lấy generation id từ completion cuối cùng (nếu có) để ghi log. Tính tổng cost USD từ tất cả completion (nếu có) để ghi log.
            var promptTokens = completionsForUsage.Sum(c => c.Usage?.InputTokenCount ?? 0);
            var completionTokens = completionsForUsage.Sum(c => c.Usage?.OutputTokenCount ?? 0);
            var usageCompletion = completionsForUsage.Count > 0 ? completionsForUsage[^1] : completion;
            decimal? costUsd = null;
            foreach (var c in completionsForUsage)
            {
                var piece = AiChatCompletionUsageHelper.TryGetOpenRouterCostUsd(c);
                if (piece.HasValue)
                    costUsd = (costUsd ?? 0m) + piece.Value;
            }
            //ghi log sử dụng AI
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = authorUserId,
                story_id = request.StoryId,
                chapter_id = usageLogChapterId,
                action_type = ActionType,
                model_name = model,
                generation_id = AiChatCompletionUsageHelper.GetGenerationId(usageCompletion),
                cost_usd = costUsd,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });

            // Debit token balance (clamp >= 0).
            try { UserDAO.DebitAiTokenLimit(authorUserId, promptTokens + completionTokens); } catch { /* best-effort */ }
            //trả kết quả cuối 
            return new SuggestNextChapterResponse
            {
                Suggestions = dtoList,
                ContextUsed = new SuggestNextChapterContextDto
                {
                    StoryTitle = story.title,
                    ChaptersIncluded = 0
                },
                ContextWarning = contextWarning
            };
        }

        private static List<NextChapterSuggestionItemDto> BuildUniqueSuggestionDtos(
            IReadOnlyList<JsonSuggestion> suggestions,
            HashSet<string> existingTitles)
        {
            var reservedTitles = new HashSet<string>(existingTitles, StringComparer.OrdinalIgnoreCase);
            var result = new List<NextChapterSuggestionItemDto>();
            foreach (var s in suggestions.Take(3))
            {
                var baseTitle = string.IsNullOrWhiteSpace(s.Title) ? "Chương tiếp theo" : s.Title.Trim();
                var title = AllocateUniqueTitle(baseTitle, reservedTitles);
                result.Add(new NextChapterSuggestionItemDto
                {
                    Title = title,
                    Summary = s.Summary ?? "",
                    Direction = s.Direction ?? "",
                    KeyEvents = s.KeyEvents,
                    CharactersInvolved = s.CharactersInvolved
                });
            }
            return result;
        }

        private static string AllocateUniqueTitle(string baseTitle, HashSet<string> reservedTitles)
        {
            baseTitle = string.IsNullOrWhiteSpace(baseTitle) ? "Chương tiếp theo" : baseTitle.Trim();
            if (reservedTitles.Add(baseTitle))
                return baseTitle;

            var n = 2;
            while (true)
            {
                var candidate = $"{baseTitle} ({n})";
                if (reservedTitles.Add(candidate))
                    return candidate;
                n++;
            }
        }
        //
        private static int ResolveSuggestTargetOrderIndex(SuggestNextChapterRequest request, List<chapters> allOrdered)
        {
            if (request.ChapterId.HasValue)
            {
                var ch = allOrdered.FirstOrDefault(c => c.id == request.ChapterId.Value);
                if (ch != null)
                    return ch.order_index;
            }

            if (request.UpToChapterId.HasValue)
            {
                var after = allOrdered.FirstOrDefault(c => c.id == request.UpToChapterId.Value);
                if (after != null)
                    return after.order_index + 1;
            }

            return allOrdered.Count == 0 ? 0 : allOrdered.Max(c => c.order_index) + 1;
        }

        private static string GetSystemPrompt()
        {
            return """
Bạn là trợ lý sáng tác cho tác giả truyện. Bạn sẽ nhận DỮ LIỆU TỪ CƠ SỞ DỮ LIỆU gồm: thông tin truyện, các đoạn RAG (nội dung chương đã có), Character Memory (trạng thái nhân vật: còn sống, chết, mất tích...), Event Memory (timeline sự kiện đã xảy ra), Story State. Phải tôn trọng tất cả: không gợi ý nhân vật đã chết/mất tích trong Character Memory xuất hiện hay hành động; không đảo ngược hoặc lặp lại sự kiện đã có trong Event Memory; chỉ gợi ý nội dung tiếp theo trên dòng thời gian.

Quan trọng — Dòng thời gian: Dữ liệu đó mô tả phần truyện ĐÃ XẢY RA (các chương đã được viết). Bạn chỉ được gợi ý nội dung cho CHƯƠNG TIẾP THEO — tức là sự kiện xảy ra SAU điểm kết thúc hiện tại của truyện. Tuyệt đối không gợi ý tình tiết đã xảy ra ở các chương trước (ví dụ: nếu trong dữ liệu đã nêu một sự kiện như đám tang đã diễn ra, thì không được gợi ý chương này "làm rõ việc hấp hối" hay quay lại thời điểm trước đó). Ưu tiên bám sát các chương/các đoạn gần nhất trong dữ liệu để xác định "điểm hiện tại" của truyện rồi gợi ý phần tiếp nối.

Dựa CHÍNH XÁC vào dữ liệu đó để đưa ra đúng 3 hướng đi KHÁC NHAU, CHI TIẾT cho chương tiếp theo (sau điểm kết thúc hiện tại).

Mỗi gợi ý phải đủ chi tiết để tác giả hình dung rõ và có thể viết ngay:
- title: Tiêu đề gợi ý cho chương (rõ ràng, gợi mở).
- summary: 2–4 câu tóm tắt hướng đi (nêu rõ bối cảnh, xung đột hoặc bước ngoặt chính).
- direction: 4–6 câu (hoặc bullet) mô tả chi tiết: diễn biến có thể có, cảm xúc nhân vật, cách nối với chương trước, tone/không khí.
- key_events: 2–4 sự kiện chính sẽ xảy ra trong chương (mỗi sự kiện một dòng, có thể đánh số 1. 2. 3.).
- characters_involved: Nhân vật chính xuất hiện hoặc liên quan, và vai trò ngắn (vd. "A – đối mặt với quyết định; B – phản ứng từ xa").

Trả về DUY NHẤT một JSON hợp lệ, không markdown:
{
  "suggestions": [
    {
      "title": "Tiêu đề gợi ý",
      "summary": "2-4 câu tóm tắt đầy đủ hướng đi, bối cảnh và xung đột.",
      "direction": "4-6 câu hoặc bullet mô tả chi tiết diễn biến, cảm xúc, cách nối với phần trước.",
      "key_events": "1. Sự kiện đầu.\n2. Sự kiện thứ hai.\n3. ...",
      "characters_involved": "Tên nhân vật – vai trò ngắn; ..."
    },
    { ... },
    { ... }
  ]
}

Yêu cầu: Đảm bảo 3 gợi ý thực sự khác nhau (khác tình tiết, xung đột hoặc kết cục); mỗi gợi ý phải đủ dài và cụ thể, không sơ sài; bám sát mạch truyện và đặc biệt nội dung các chương/đoạn gần nhất trong dữ liệu — chỉ gợi ý nội dung tiếp theo trên dòng thời gian, không đảo ngược hay lặp lại sự kiện đã xảy ra. Ngôn ngữ: Toàn bộ nội dung sinh ra (title, summary, direction, key_events, characters_involved) phải bằng tiếng Việt; không xen từ hoặc cụm từ ngôn ngữ khác.
""" + "\n\n" + SuggestConstitutionalRules;
        }

        private static string GetUserPrompt(stories story, string contextBlock, string languageInstruction)
        {
            return $"{contextBlock}\n\n---\n{languageInstruction}\n\nGợi ý đúng 3 hướng đi KHÁC NHAU và CHI TIẾT cho chương tiếp theo (chỉ nội dung xảy ra SAU điểm kết thúc hiện tại của truyện trong dữ liệu trên; không gợi ý sự kiện đã xảy ra). Mỗi gợi ý phải có summary 2–4 câu, direction 4–6 câu/bullet, key_events và characters_involved đầy đủ. Trả về JSON theo đúng cấu trúc đã nêu.";
        }

        private static List<JsonSuggestion> ParseSuggestions(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new List<JsonSuggestion>();
            text = text.Trim();

            // Bỏ markdown code block (```json ... ``` hoặc ``` ... ```)
            if (text.StartsWith("```"))
            {
                var firstNewline = text.IndexOf('\n');
                var start = firstNewline >= 0 ? firstNewline + 1 : 3;
                var end = text.IndexOf("```", start, StringComparison.Ordinal);
                if (end > start)
                    text = text[start..end].Trim();
                else
                    text = text[start..].Trim();
            }

            // Lấy đoạn JSON: từ ký tự '{' hoặc '[' đầu tiên đến '}' hoặc ']' tương ứng (tránh chữ thừa trước/sau)
            var firstBrace = text.IndexOf('{');
            var firstBracket = text.IndexOf('[');
            int startIdx;
            int endIdx;
            if (firstBracket >= 0 && (firstBrace < 0 || firstBracket < firstBrace))
            {
                startIdx = firstBracket;
                endIdx = text.LastIndexOf(']');
            }
            else
            {
                startIdx = firstBrace;
                endIdx = text.LastIndexOf('}');
            }
            if (startIdx >= 0 && endIdx > startIdx)
                text = text.Substring(startIdx, endIdx - startIdx + 1);

            // Chuẩn hóa newline trong string value (chỉ trong "...") để JSON parse được
            text = NormalizeNewlinesInJsonStrings(text);

            try
            {
                var options = new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
                var root = JsonDocument.Parse(text, options).RootElement;
                JsonElement arrProp;
                if (root.ValueKind == JsonValueKind.Array)
                    arrProp = root;
                else if (root.TryGetProperty("suggestions", out arrProp) || root.TryGetProperty("Suggestions", out arrProp)
                         || root.TryGetProperty("data", out arrProp) || root.TryGetProperty("items", out arrProp))
                { /* arrProp đã được gán */ }
                else
                    return new List<JsonSuggestion>();
                if (arrProp.ValueKind != JsonValueKind.Array)
                    return new List<JsonSuggestion>();

                var list = new List<JsonSuggestion>();
                foreach (var item in arrProp.EnumerateArray())
                {
                    var s = new JsonSuggestion();
                    if (item.TryGetProperty("title", out var t)) s.Title = t.GetString();
                    if (item.TryGetProperty("Title", out t)) s.Title ??= t.GetString();
                    if (item.TryGetProperty("summary", out var sv)) s.Summary = GetStringFromElement(sv);
                    if (item.TryGetProperty("Summary", out sv)) s.Summary ??= GetStringFromElement(sv);
                    if (item.TryGetProperty("direction", out var d)) s.Direction = GetStringFromElement(d);
                    if (item.TryGetProperty("Direction", out d)) s.Direction ??= GetStringFromElement(d);
                    if (item.TryGetProperty("key_events", out var k)) s.KeyEvents = GetStringFromElement(k);
                    if (item.TryGetProperty("KeyEvents", out k)) s.KeyEvents ??= GetStringFromElement(k);
                    if (item.TryGetProperty("keyEvents", out k)) s.KeyEvents ??= GetStringFromElement(k);
                    if (item.TryGetProperty("characters_involved", out var c)) s.CharactersInvolved = GetStringFromElement(c);
                    if (item.TryGetProperty("charactersInvolved", out c)) s.CharactersInvolved ??= GetStringFromElement(c);
                    if (item.TryGetProperty("CharactersInvolved", out c)) s.CharactersInvolved ??= GetStringFromElement(c);

                    if (!string.IsNullOrWhiteSpace(s.Title) || !string.IsNullOrWhiteSpace(s.Summary) || !string.IsNullOrWhiteSpace(s.Direction))
                        list.Add(s);
                }
                return list;
            }
            catch
            {
                return new List<JsonSuggestion>();
            }
        }

        private static async Task<(List<JsonSuggestion> Suggestions, string Text, ChatCompletion? Completion)> RetryNormalizeSuggestionsJsonAsync(
            ChatClient client,
            ChatCompletionOptions? options,
            string systemPrompt,
            string rawResponse,
            CancellationToken cancellationToken)
        {
            var normalizedRaw = rawResponse ?? string.Empty;
            if (normalizedRaw.Length > JsonRepairMaxInputChars)
                normalizedRaw = normalizedRaw[..JsonRepairMaxInputChars];

            var userPrompt = """
Phản hồi trước chưa đúng format JSON mong muốn.
Hãy CHỈ trả về duy nhất JSON hợp lệ theo schema:
{
  "suggestions": [
    {
      "title": "string",
      "summary": "string",
      "direction": "string",
      "key_events": "string",
      "characters_involved": "string"
    },
    {
      "title": "string",
      "summary": "string",
      "direction": "string",
      "key_events": "string",
      "characters_involved": "string"
    },
    {
      "title": "string",
      "summary": "string",
      "direction": "string",
      "key_events": "string",
      "characters_involved": "string"
    }
  ]
}

Yêu cầu:
- Không markdown, không ```json, không giải thích.
- Giữ đúng 3 phần tử trong suggestions.
- Mọi trường là chuỗi.

Phản hồi trước cần chuẩn hóa:
""" + "\n" + normalizedRaw;

            var retryMessages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            try
            {
                var completion = await client.CompleteChatAsync(retryMessages, options, cancellationToken).ConfigureAwait(false);
                var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
                if (string.IsNullOrWhiteSpace(text))
                    return (new List<JsonSuggestion>(), rawResponse, completion.Value);
                var parsed = ParseSuggestions(text);
                return (parsed, text, completion.Value);
            }
            catch
            {
                return (new List<JsonSuggestion>(), rawResponse, null);
            }
        }

        /// <summary>Lấy string từ JsonElement: nếu là string trả về giá trị; nếu là array (vd. ["a","b"]) thì nối bằng newline.</summary>
        private static string? GetStringFromElement(JsonElement el)
        {
            if (el.ValueKind == JsonValueKind.String)
                return el.GetString();
            if (el.ValueKind == JsonValueKind.Array)
            {
                var parts = new List<string>();
                foreach (var e in el.EnumerateArray())
                    if (e.ValueKind == JsonValueKind.String && e.GetString() is { } str)
                        parts.Add(str);
                return parts.Count > 0 ? string.Join("\n", parts) : null;
            }
            return null;
        }

        /// <summary>Thay newline thật nằm trong string value (giữa hai dấu ") bằng space để JSON parse được.</summary>
        private static string NormalizeNewlinesInJsonStrings(string json)
        {
            var result = new StringBuilder(json.Length);
            var i = 0;
            var inString = false;
            var escape = false;
            var quote = '"';
            while (i < json.Length)
            {
                var c = json[i];
                if (escape)
                {
                    result.Append(c);
                    escape = false;
                    i++;
                    continue;
                }
                if (c == '\\')
                {
                    result.Append(c);
                    escape = true;
                    i++;
                    continue;
                }
                if (inString && (c == '\n' || c == '\r'))
                {
                    result.Append(' ');
                    if (c == '\r' && i + 1 < json.Length && json[i + 1] == '\n') i++;
                    i++;
                    continue;
                }
                // Chỉ coi dấu ngoặc kép " là ranh giới chuỗi JSON (tránh nhầm dấu nháy đơn trong nội dung)
                if (c == '"' && !inString)
                {
                    inString = true;
                    quote = c;
                    result.Append(c);
                    i++;
                    continue;
                }
                if (c == quote)
                {
                    inString = false;
                    result.Append(c);
                    i++;
                    continue;
                }
                result.Append(c);
                i++;
            }
            return result.ToString();
        }

        private class JsonSuggestion
        {
            public string? Title { get; set; }
            public string? Summary { get; set; }
            public string? Direction { get; set; }
            public string? KeyEvents { get; set; }
            public string? CharactersInvolved { get; set; }
        }
    }
}
