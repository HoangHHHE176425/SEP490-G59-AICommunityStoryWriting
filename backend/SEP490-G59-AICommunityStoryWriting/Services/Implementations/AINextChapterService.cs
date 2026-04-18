using System.Text;
using System.Text.Json;
using System.ClientModel;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AINextChapterService : IAINextChapterService
    {
        private const string ActionType = "SUGGEST_NEXT_CHAPTER";

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IStoryRagService _ragService;
        private readonly IStoryMemoryEngine _memoryEngine;
        private readonly IAIUsageLogRepository _aiUsageLogRepository;
        private readonly IConfiguration _configuration;
        private readonly IUserLookup _userLookup;

        public AINextChapterService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IStoryRagService ragService,
            IStoryMemoryEngine memoryEngine,
            IAIUsageLogRepository aiUsageLogRepository,
            IConfiguration configuration,
            IUserLookup userLookup)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _ragService = ragService;
            _memoryEngine = memoryEngine;
            _aiUsageLogRepository = aiUsageLogRepository;
            _configuration = configuration;
            _userLookup = userLookup;
        }

        public async Task<SuggestNextChapterResponse> SuggestNextChapterAsync(
            SuggestNextChapterRequest request,
            Guid authorUserId,
            CancellationToken cancellationToken = default)
        {
            var story = _storyRepository.GetById(request.StoryId);
            if (story == null)
                throw new InvalidOperationException("Truyện không tồn tại.");

            if (story.author_id != authorUserId)
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được sử dụng tính năng gợi ý chương.");

            if (_userLookup.IsAuthorWritingSuspended(authorUserId))
                throw new InvalidOperationException("Tài khoản đang bị tạm khóa chức năng viết truyện/chương (compliance/admin), không thể dùng gợi ý AI.");

            var allChaptersOrdered = _chapterRepository.GetByStoryId(request.StoryId).OrderBy(c => c.order_index).ToList();
            var targetOrderForWarning = ResolveSuggestTargetOrderIndex(request, allChaptersOrdered);
            var contextWarning = ChapterAiContextWarningHelper.GetWarningIfApplicable(allChaptersOrdered, targetOrderForWarning);

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

            await _ragService.TryEnsureIndexedAsync(request.StoryId, afterChapterId: request.AfterChapterId, cancellationToken);
            if (!_ragService.IsRagAvailableForStory(request.StoryId))
            {
                throw new InvalidOperationException(
                    "Truyện chưa có chỉ mục RAG (chưa có chunk vector). Đảm bảo chương PUBLISHED có nội dung sau khi chunk, rồi gọi POST /api/ai/index-rag hoặc thử lại — kiểm tra thư mục VectorStore (Data/faiss) có quyền ghi trên server.");
            }

            var lastChapterContent = chapters.LastOrDefault()?.content ?? "";
            var ragQuery = $"{story.summary ?? ""} {lastChapterContent}".Trim();
            var contextBlock = await _memoryEngine.BuildContextForSuggestAsync(request.StoryId, ragQuery, cancellationToken);

            var languageInstruction = StoryLanguageHelper.VietnameseOnlyInstruction;

            var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlanner);
            var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

            var systemPrompt = GetSystemPrompt();
            var userPrompt = GetUserPrompt(story, contextBlock, languageInstruction);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var balanceNow = Math.Max(0L, UserDAO.GetAiTokenLimit(authorUserId));
            // Prompt/input thực tế đang quanh ~2.5k token, reserve mặc định cao hơn để tránh vượt số dư.
            var reservedInputTokens = _configuration.GetValue("AI:SuggestReservedInputTokens", 2600);
            var cappedOutputTokens = (int)Math.Max(64, balanceNow - reservedInputTokens);
            var options = AIClientHelper.GetCompletionOptions(_configuration, null, cappedOutputTokens);
            ChatCompletion completion;
            try
            {
                completion = await client.CompleteChatAsync(messages, options);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Không thể kết nối dịch vụ AI. Vui lòng thử lại sau hoặc kiểm tra cấu hình API key.",
                    ex);
            }

            var text = completion.Content?.Count > 0 ? completion.Content[0].Text : null;
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("AI không trả về nội dung gợi ý.");

            var suggestions = ParseSuggestions(text);
            if (suggestions.Count == 0)
            {
                var snippet = text.Length > 600 ? text[..600] + "..." : text;
                throw new InvalidOperationException(
                    "Không thể đọc được gợi ý từ phản hồi AI. Kiểm tra model trả về đúng JSON với mảng \"suggestions\" (title, summary, direction, key_events, characters_involved). Phản hồi AI (rút gọn): " + snippet);
            }

            var existingTitles = allChaptersOrdered
                .Select(c => c.title?.Trim())
                .Where(t => !string.IsNullOrWhiteSpace(t))
                .Select(t => t!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var dtoList = BuildUniqueSuggestionDtos(suggestions, existingTitles);

            Guid? usageLogChapterId = chapters.LastOrDefault()?.id;
            if (request.ChapterId.HasValue)
            {
                var targetChapter = _chapterRepository.GetById(request.ChapterId.Value);
                if (targetChapter != null && targetChapter.story_id != request.StoryId)
                    throw new InvalidOperationException("ChapterId không khớp truyện.");
                if (targetChapter != null)
                    usageLogChapterId = targetChapter.id;
            }

            var promptTokens = completion.Usage?.InputTokenCount ?? 0;
            var completionTokens = completion.Usage?.OutputTokenCount ?? 0;

            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = authorUserId,
                story_id = request.StoryId,
                chapter_id = usageLogChapterId,
                action_type = ActionType,
                model_name = model,
                generation_id = AiChatCompletionUsageHelper.GetGenerationId(completion),
                cost_usd = AiChatCompletionUsageHelper.TryGetOpenRouterCostUsd(completion),
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });

            // Debit token balance (clamp >= 0).
            try { UserDAO.DebitAiTokenLimit(authorUserId, promptTokens + completionTokens); } catch { /* best-effort */ }

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

        private static int ResolveSuggestTargetOrderIndex(SuggestNextChapterRequest request, List<chapters> allOrdered)
        {
            if (request.ChapterId.HasValue)
            {
                var ch = allOrdered.FirstOrDefault(c => c.id == request.ChapterId.Value);
                if (ch != null)
                    return ch.order_index;
            }

            if (request.AfterChapterId.HasValue)
            {
                var after = allOrdered.FirstOrDefault(c => c.id == request.AfterChapterId.Value);
                if (after != null)
                    return after.order_index + 1;
            }

            return allOrdered.Count == 0 ? 0 : allOrdered.Max(c => c.order_index) + 1;
        }

        private const string DbContextLabel = "=== DỮ LIỆU TỪ CƠ SỞ DỮ LIỆU (ngữ cảnh truyện: RAG, Character Memory, Event Memory, Story State) — Dùng làm tham chiếu bắt buộc ===";

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
""";
        }

        private static string GetUserPrompt(stories story, string contextBlock, string languageInstruction)
        {
            return $"{DbContextLabel}\n\n{contextBlock}\n\n---\n{languageInstruction}\n\nGợi ý đúng 3 hướng đi KHÁC NHAU và CHI TIẾT cho chương tiếp theo (chỉ nội dung xảy ra SAU điểm kết thúc hiện tại của truyện trong dữ liệu trên; không gợi ý sự kiện đã xảy ra). Mỗi gợi ý phải có summary 2–4 câu, direction 4–6 câu/bullet, key_events và characters_involved đầy đủ. Trả về JSON theo đúng cấu trúc đã nêu.";
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
