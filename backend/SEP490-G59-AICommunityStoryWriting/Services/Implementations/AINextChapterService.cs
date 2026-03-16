using System.Text;
using System.Text.Json;
using System.ClientModel;
using BusinessObjects.Entities;
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
        private readonly IAIUsageLogRepository _aiUsageLogRepository;
        private readonly IConfiguration _configuration;

        public AINextChapterService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IStoryRagService ragService,
            IAIUsageLogRepository aiUsageLogRepository,
            IConfiguration configuration)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _ragService = ragService;
            _aiUsageLogRepository = aiUsageLogRepository;
            _configuration = configuration;
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

            var chapters = _chapterRepository.GetByStoryId(request.StoryId)
                .OrderBy(c => c.order_index)
                .ToList();
            var hasContent = chapters.Any(c => !string.IsNullOrWhiteSpace(c.content));
            if (!hasContent)
                throw new InvalidOperationException("Truyện cần có ít nhất một chương đã có nội dung để gợi ý chương tiếp theo.");

            await _ragService.TryEnsureIndexedAsync(request.StoryId, afterChapterId: null, cancellationToken);
            if (!_ragService.IsRagAvailableForStory(request.StoryId))
                throw new InvalidOperationException("Truyện chưa được index RAG. Vui lòng cấu hình embedding (AI:EmbeddingBaseUrl, EmbeddingModel) và đảm bảo truyện có chương có nội dung.");

            var lastChapterContent = chapters.LastOrDefault()?.content ?? "";
            var query = $"{story.summary ?? ""} {lastChapterContent}".Trim();
            var ragBlock = await _ragService.RetrieveContextAsync(request.StoryId, query, maxChars: 8000, topK: 15, cancellationToken);
            if (string.IsNullOrWhiteSpace(ragBlock))
                throw new InvalidOperationException("Không lấy được ngữ cảnh từ RAG. Đảm bảo truyện đã có chương có nội dung và cấu hình embedding đúng.");
            var contextBlock = BuildContextBlockFromRag(story, ragBlock);

            var storyLanguage = StoryLanguageHelper.DetectFromStoryContext(contextBlock);
            var languageInstruction = StoryLanguageHelper.GetLanguageInstruction(storyLanguage);

            var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfig(_configuration);
            var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

            var systemPrompt = GetSystemPrompt();
            var userPrompt = GetUserPrompt(story, contextBlock, languageInstruction);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            var options = AIClientHelper.GetCompletionOptions(_configuration, null);
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
                throw new InvalidOperationException("Không thể đọc được gợi ý từ phản hồi AI.");

            var promptTokens = completion.Usage?.InputTokenCount ?? 0;
            var completionTokens = completion.Usage?.OutputTokenCount ?? 0;

            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = authorUserId,
                story_id = request.StoryId,
                chapter_id = chapters.LastOrDefault()?.id,
                action_type = ActionType,
                model_name = model,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });

            return new SuggestNextChapterResponse
            {
                Suggestions = suggestions.Take(3).Select(s => new NextChapterSuggestionItemDto
                {
                    Title = s.Title ?? "Chương tiếp theo",
                    Summary = s.Summary ?? "",
                    Direction = s.Direction ?? "",
                    KeyEvents = s.KeyEvents,
                    CharactersInvolved = s.CharactersInvolved
                }).ToList(),
                ContextUsed = new SuggestNextChapterContextDto
                {
                    StoryTitle = story.title,
                    ChaptersIncluded = 0
                }
            };
        }

        private static string BuildContextBlockFromRag(stories story, string ragBlock)
        {
            var lines = new List<string>
            {
                $"## Truyện: {story.title}",
                string.IsNullOrWhiteSpace(story.summary) ? "" : $"Tóm tắt: {story.summary}",
                "## Các đoạn liên quan từ truyện (RAG):",
                ragBlock
            };
            return string.Join("\n\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private const string DbContextLabel = "=== DỮ LIỆU TỪ CƠ SỞ DỮ LIỆU (ngữ cảnh truyện: thông tin truyện, nội dung các chương / RAG) — Dùng làm tham chiếu bắt buộc ===";

        private static string GetSystemPrompt()
        {
            return """
Bạn là trợ lý sáng tác cho tác giả truyện. Bạn sẽ nhận DỮ LIỆU TỪ CƠ SỞ DỮ LIỆU (ngữ cảnh truyện: thông tin truyện, nội dung các chương đã có hoặc các đoạn RAG).

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

Yêu cầu: Đảm bảo 3 gợi ý thực sự khác nhau (khác tình tiết, xung đột hoặc kết cục); mỗi gợi ý phải đủ dài và cụ thể, không sơ sài; bám sát mạch truyện và đặc biệt nội dung các chương/đoạn gần nhất trong dữ liệu — chỉ gợi ý nội dung tiếp theo trên dòng thời gian, không đảo ngược hay lặp lại sự kiện đã xảy ra; ngôn ngữ trùng với ngôn ngữ của truyện (ưu tiên tiếng Việt nếu truyện tiếng Việt).
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

            // Lấy đoạn JSON: từ ký tự '{' đầu tiên đến '}' cuối cùng (tránh chữ thừa trước/sau)
            var firstBrace = text.IndexOf('{');
            var lastBrace = text.LastIndexOf('}');
            if (firstBrace >= 0 && lastBrace > firstBrace)
                text = text.Substring(firstBrace, lastBrace - firstBrace + 1);

            // Chuẩn hóa newline trong string value: một số model trả xuống dòng thật trong chuỗi → JSON lỗi. Thay \r\n và \n thành khoảng trắng trong chuỗi nằm trong "..."
            text = NormalizeNewlinesInJsonStrings(text);

            try
            {
                var root = JsonDocument.Parse(text).RootElement;
                // Chấp nhận "suggestions" hoặc "Suggestions"
                if (!root.TryGetProperty("suggestions", out var arrProp))
                    root.TryGetProperty("Suggestions", out arrProp);
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
                    if (item.TryGetProperty("keyEvents", out k)) s.KeyEvents ??= GetStringFromElement(k);
                    if (item.TryGetProperty("characters_involved", out var c)) s.CharactersInvolved = GetStringFromElement(c);
                    if (item.TryGetProperty("charactersInvolved", out c)) s.CharactersInvolved ??= GetStringFromElement(c);

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
                if ((c == '"' || c == '\'') && !inString)
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
