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
            var lastChapterContent = chapters.LastOrDefault()?.content ?? "";
            var query = $"{story.summary ?? ""} {lastChapterContent}".Trim();

            if (!_ragService.IsRagAvailableForStory(request.StoryId))
                throw new InvalidOperationException("RAG chưa sẵn sàng cho truyện này. Hãy gọi POST /api/ai/index-rag với storyId trước khi gợi ý chương.");

            var ragBlock = await _ragService.RetrieveContextAsync(request.StoryId, query, maxChars: 8000, topK: 15, cancellationToken);
            if (string.IsNullOrWhiteSpace(ragBlock))
                throw new InvalidOperationException("Không lấy được ngữ cảnh từ RAG. Đảm bảo truyện đã có chương có nội dung và đã gọi POST /api/ai/index-rag.");

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
                    Direction = s.Direction ?? ""
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

        private static string GetSystemPrompt()
        {
            return """
Bạn là trợ lý sáng tác cho tác giả truyện. Dựa trên thông tin truyện và ngữ cảnh (Story memory hoặc RAG), đưa ra đúng 3 hướng đi KHÁC NHAU cho chương tiếp theo (tình tiết, bước ngoặt, cảm xúc/kết cục).

Trả về DUY NHẤT một JSON hợp lệ, không markdown:
{
  "suggestions": [
    {
      "title": "Tiêu đề gợi ý cho chương tiếp theo",
      "summary": "1-2 câu tóm tắt ngắn hướng đi",
      "direction": "Mô tả chi tiết hơn: tình tiết, cảm xúc, nhân vật, cách nối với phần trước"
    },
    { ... },
    { ... }
  ]
}

Yêu cầu: Đảm bảo 3 gợi ý thực sự khác nhau; ngôn ngữ trùng với ngôn ngữ của truyện (Việt hoặc Anh).
""";
        }

        private static string GetUserPrompt(stories story, string contextBlock, string languageInstruction)
        {
            return $"Ngữ cảnh truyện:\n\n{contextBlock}\n\n{languageInstruction}\n\nGợi ý đúng 3 hướng đi khác nhau cho chương tiếp theo. Trả về JSON theo cấu trúc đã nêu.";
        }

        private static List<JsonSuggestion> ParseSuggestions(string text)
        {
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                var start = text.IndexOf('\n') + 1;
                var end = text.IndexOf("```", start, StringComparison.Ordinal);
                if (end > start)
                    text = text[start..end];
            }

            try
            {
                var root = JsonDocument.Parse(text).RootElement;
                var arr = root.GetProperty("suggestions");
                var list = new List<JsonSuggestion>();
                foreach (var item in arr.EnumerateArray())
                {
                    list.Add(new JsonSuggestion
                    {
                        Title = item.TryGetProperty("title", out var t) ? t.GetString() : null,
                        Summary = item.TryGetProperty("summary", out var s) ? s.GetString() : null,
                        Direction = item.TryGetProperty("direction", out var d) ? d.GetString() : null
                    });
                }
                return list;
            }
            catch
            {
                return new List<JsonSuggestion>();
            }
        }

        private class JsonSuggestion
        {
            public string? Title { get; set; }
            public string? Summary { get; set; }
            public string? Direction { get; set; }
        }
    }
}
