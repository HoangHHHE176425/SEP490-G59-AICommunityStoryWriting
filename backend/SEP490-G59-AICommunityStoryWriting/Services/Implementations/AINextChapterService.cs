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
        private const int MaxChaptersToSend = 2;
        private const int MaxCharsPerChapter = 2000;
        private const string ActionType = "SUGGEST_NEXT_CHAPTER";

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IAIUsageLogRepository _aiUsageLogRepository;
        private readonly IConfiguration _configuration;

        public AINextChapterService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IAIUsageLogRepository aiUsageLogRepository,
            IConfiguration configuration)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
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

            IEnumerable<chapters> chaptersForContext = chapters;
            if (request.AfterChapterId.HasValue)
            {
                var afterIdx = chapters.FirstOrDefault(c => c.id == request.AfterChapterId.Value)?.order_index;
                if (afterIdx.HasValue)
                    chaptersForContext = chapters.Where(c => c.order_index <= afterIdx.Value);
            }

            var lastChapters = chaptersForContext.TakeLast(MaxChaptersToSend).ToList();
            var contextBlock = BuildContextBlock(story, lastChapters);

            var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfig(_configuration);
            var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

            var systemPrompt = GetSystemPrompt();
            var userPrompt = GetUserPrompt(story, contextBlock);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(systemPrompt),
                new UserChatMessage(userPrompt)
            };

            ChatCompletion completion;
            try
            {
                completion = await client.CompleteChatAsync(messages);
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
                chapter_id = lastChapters.LastOrDefault()?.id,
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
                    ChaptersIncluded = lastChapters.Count
                }
            };
        }

        private static string BuildContextBlock(stories story, List<chapters> lastChapters)
        {
            var lines = new List<string>
            {
                $"## Truyện: {story.title}",
                string.IsNullOrWhiteSpace(story.summary) ? "" : $"Tóm tắt: {story.summary}"
            };

            foreach (var ch in lastChapters)
            {
                var content = ch.content ?? "";
                if (content.Length > MaxCharsPerChapter)
                    content = content[..MaxCharsPerChapter] + "...";
                lines.Add($"### Chương {ch.order_index}: {ch.title}");
                lines.Add(content);
            }

            return string.Join("\n\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private static string GetSystemPrompt()
        {
            return """
Bạn là trợ lý sáng tác cho tác giả truyện. Nhiệm vụ: dựa trên thông tin truyện và các chương gần nhất, đưa ra đúng 3 hướng đi KHÁC NHAU cho chương tiếp theo (mỗi hướng có thể là tình tiết khác nhau, bước ngoặt khác nhau, hoặc cảm xúc/kết cục khác).

Trả về DUY NHẤT một JSON hợp lệ, không kèm markdown hay giải thích, theo cấu trúc:
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

        private static string GetUserPrompt(stories story, string contextBlock)
        {
            return $"Dưới đây là thông tin truyện và các chương gần nhất.\n\n{contextBlock}\n\nHãy gợi ý đúng 3 hướng đi khác nhau cho chương tiếp theo, trả về JSON theo đúng cấu trúc đã nêu.";
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
