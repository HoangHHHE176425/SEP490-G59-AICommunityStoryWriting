using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Repositories;
using Services.DTOs.Ai;
using Services.Exceptions;
using Services.Interfaces;

namespace Services.Implementations;

public class StoryAiService : IStoryAiService
{
    private const int MaxChaptersInContext = 5;
    private const int MaxCharsPerChapter = 1200;
    private const string DefaultModel = "gemini-2.0-flash";
    private const string GeminiBaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const int DefaultCacheExpirationHours = 24;

    private static readonly ConcurrentDictionary<string, (SuggestNextChapterResponseDto Response, DateTimeOffset ExpiresAt)> SuggestionCache = new();

    private readonly IChapterRepository _chapterRepository;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StoryAiService> _logger;
    private readonly StoryPlatformDbContext? _dbContext;

    public StoryAiService(
        IChapterRepository chapterRepository,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<StoryAiService> logger,
        StoryPlatformDbContext? dbContext = null)
    {
        _chapterRepository = chapterRepository;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _dbContext = dbContext;
    }

    public async Task<SuggestNextChapterResponseDto?> SuggestNextChapterAsync(
        Guid storyId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        CheckUserRateLimit(userId);

        var story = StoryDAO.GetById(storyId);
        if (story == null)
        {
            _logger.LogInformation("Story {StoryId} not found.", storyId);
            return null;
        }

        var chapters = _chapterRepository
            .GetByStoryId(storyId)
            .OrderBy(c => c.order_index)
            .ToList();

        if (chapters.Count == 0)
        {
            _logger.LogInformation("Story {StoryId} has no chapters.", storyId);
            return null;
        }

        var nextChapterNumber = chapters.Count + 1;

        // Chế độ demo: luôn trả gợi ý mẫu, không gọi Gemini (an toàn khi bảo vệ đồ án).
        if (_configuration.GetValue("Gemini:DemoMode", false))
        {
            _logger.LogInformation("Gemini DemoMode: trả gợi ý mẫu cho story {StoryId}.", storyId);
            return BuildMockResponse(story, nextChapterNumber, isDemo: true);
        }

        // Cache: cùng truyện + cùng số chương → trả từ cache, không tốn quota.
        var cacheKey = $"{storyId:N}_{nextChapterNumber}";
        var cacheHours = _configuration.GetValue("Gemini:SuggestionCacheExpirationHours", DefaultCacheExpirationHours);
        if (SuggestionCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTimeOffset.UtcNow)
        {
            _logger.LogInformation("Suggest-next-chapter cache hit cho {StoryId}, chương {Next}.", storyId, nextChapterNumber);
            return CloneCachedResponse(cached.Response);
        }

        var apiKey = _configuration["Gemini:ApiKey"] ?? _configuration["Ai:Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Gemini API key not configured. Set Gemini:ApiKey or Ai:Gemini:ApiKey.");
            return null;
        }

        var recentChapters = chapters.TakeLast(MaxChaptersInContext).ToList();
        var prompt = BuildSuggestNextChapterPrompt(story, recentChapters);

        try
        {
            var (content, modelUsed, promptTokens, completionTokens) = await CallGeminiGenerateContentAsync(
                apiKey,
                prompt.SystemMessage,
                prompt.UserMessage,
                cancellationToken);

            if (string.IsNullOrWhiteSpace(content))
            {
                _logger.LogWarning("Gemini returned empty content for story {StoryId}.", storyId);
                return null;
            }

            var suggestions = ParseSuggestionsFromJson(content);

            if (suggestions.Count == 0)
            {
                _logger.LogWarning("Could not parse suggestions from Gemini response for story {StoryId}. Raw: {Raw}", storyId, content.Length > 500 ? content[..500] : content);
            }

            TryLogUsage(userId, storyId, null, "SUGGEST_NEXT_CHAPTER", modelUsed ?? DefaultModel, promptTokens ?? 0, completionTokens ?? 0, "OK");

            var response = new SuggestNextChapterResponseDto
            {
                StoryId = storyId,
                StoryTitle = story.title ?? "",
                NextChapterNumber = nextChapterNumber,
                Suggestions = suggestions,
                ModelUsed = modelUsed ?? DefaultModel,
                PromptTokens = promptTokens,
                CompletionTokens = completionTokens,
                IsFallbackOrDemo = false
            };

            // Lưu cache để lần sau cùng ngữ cảnh không gọi Gemini.
            var expiresAt = DateTimeOffset.UtcNow.AddHours(cacheHours);
            SuggestionCache.AddOrUpdate(cacheKey, (response, expiresAt), (_, _) => (response, expiresAt));

            return response;
        }
        catch (HttpRequestException ex)
        {
            var msg = ex.Message ?? "";
            if (msg.Contains("429") || msg.Contains("Too Many Requests") || msg.Contains("RESOURCE_EXHAUSTED") || msg.Contains("quota"))
            {
                var openAiKey = _configuration["OpenAI:ApiKey"] ?? _configuration["Ai:OpenAI:ApiKey"];
                if (!string.IsNullOrWhiteSpace(openAiKey))
                {
                    try
                    {
                        var openAiResult = await CallOpenAIChatAsync(openAiKey, prompt.SystemMessage, prompt.UserMessage, cancellationToken);
                        if (!string.IsNullOrWhiteSpace(openAiResult.Content))
                        {
                            var suggestions = ParseSuggestionsFromJson(openAiResult.Content);
                            if (suggestions.Count > 0)
                            {
                                _logger.LogInformation("Gemini 429; đã dùng OpenAI fallback cho story {StoryId}.", storyId);
                                TryLogUsage(userId, storyId, null, "SUGGEST_NEXT_CHAPTER", openAiResult.ModelUsed ?? "openai", openAiResult.PromptTokens ?? 0, openAiResult.CompletionTokens ?? 0, "OK_OPENAI_FALLBACK");
                                return new SuggestNextChapterResponseDto
                                {
                                    StoryId = storyId,
                                    StoryTitle = story.title ?? "",
                                    NextChapterNumber = nextChapterNumber,
                                    Suggestions = suggestions,
                                    ModelUsed = openAiResult.ModelUsed ?? "gpt-4o-mini",
                                    PromptTokens = openAiResult.PromptTokens,
                                    CompletionTokens = openAiResult.CompletionTokens,
                                    IsFallbackOrDemo = true
                                };
                            }
                        }
                    }
                    catch (Exception openAiEx)
                    {
                        _logger.LogWarning(openAiEx, "OpenAI fallback thất bại cho story {StoryId}, trả gợi ý mẫu.", storyId);
                    }
                }
                _logger.LogWarning(ex, "Gemini quota/429 cho story {StoryId}; trả gợi ý mẫu (fallback).", storyId);
                TryLogUsage(userId, storyId, null, "SUGGEST_NEXT_CHAPTER", DefaultModel, 0, 0, "FALLBACK");
                return BuildMockResponse(story, nextChapterNumber, isFallback: true);
            }
            _logger.LogError(ex, "Gemini call failed for story {StoryId}.", storyId);
            TryLogUsage(userId, storyId, null, "SUGGEST_NEXT_CHAPTER", DefaultModel, 0, 0, "ERROR");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini call failed for story {StoryId}.", storyId);
            TryLogUsage(userId, storyId, null, "SUGGEST_NEXT_CHAPTER", DefaultModel, 0, 0, "ERROR");
            throw;
        }
    }

    private static SuggestNextChapterResponseDto CloneCachedResponse(SuggestNextChapterResponseDto src)
    {
        return new SuggestNextChapterResponseDto
        {
            StoryId = src.StoryId,
            StoryTitle = src.StoryTitle,
            NextChapterNumber = src.NextChapterNumber,
            Suggestions = src.Suggestions.ToList(),
            ModelUsed = src.ModelUsed != null ? $"{src.ModelUsed} (cache)" : "cache",
            PromptTokens = src.PromptTokens,
            CompletionTokens = src.CompletionTokens,
            IsFallbackOrDemo = false
        };
    }

    private SuggestNextChapterResponseDto BuildMockResponse(stories story, int nextChapterNumber, bool isDemo = false, bool isFallback = false)
    {
        var suggestions = GetMockSuggestions(story.title ?? "Truyện", nextChapterNumber);
        return new SuggestNextChapterResponseDto
        {
            StoryId = story.id,
            StoryTitle = story.title ?? "",
            NextChapterNumber = nextChapterNumber,
            Suggestions = suggestions,
            ModelUsed = isDemo ? "demo" : "fallback",
            PromptTokens = null,
            CompletionTokens = null,
            IsFallbackOrDemo = true
        };
    }

    private static List<NextChapterSuggestionItemDto> GetMockSuggestions(string storyTitle, int nextChapterNumber)
    {
        var title = string.IsNullOrWhiteSpace(storyTitle) ? "Truyện" : storyTitle;
        return
        [
            new NextChapterSuggestionItemDto { Title = $"Chương {nextChapterNumber}: Bước ngoặt", Description = "Nhân vật chính đối mặt quyết định quan trọng, dẫn đến thay đổi lớn trong cốt truyện." },
            new NextChapterSuggestionItemDto { Title = $"Chương {nextChapterNumber}: Hồi tưởng", Description = "Khám phá quá khứ nhân vật qua ký ức hoặc lời kể, làm rõ động cơ hành động." },
            new NextChapterSuggestionItemDto { Title = $"Chương {nextChapterNumber}: Đối đầu", Description = "Mâu thuẫn leo thang, xung đột trực tiếp giữa các phe phái hoặc nhân vật." },
            new NextChapterSuggestionItemDto { Title = $"Chương {nextChapterNumber}: Bình yên trước bão", Description = "Khoảnh khắc yên ả trước khi sự kiện lớn ập đến, tạo kịch tính." },
            new NextChapterSuggestionItemDto { Title = $"Chương {nextChapterNumber}: Kết nối mới", Description = "Nhân vật gặp gỡ đồng minh hoặc kẻ đối địch mới, mở rộng thế giới truyện." }
        ];
    }

    private const string ActionSuggestNextChapter = "SUGGEST_NEXT_CHAPTER";
    private const int DefaultMaxPerMinute = 2;
    private const int DefaultMaxPerDay = 20;

    private void CheckUserRateLimit(Guid userId)
    {
        if (_dbContext == null) return;

        var maxPerMinute = _configuration.GetValue("Gemini:MaxRequestsPerUserPerMinute", DefaultMaxPerMinute);
        var maxPerDay = _configuration.GetValue("Gemini:MaxRequestsPerUserPerDay", DefaultMaxPerDay);
        if (maxPerMinute <= 0 && maxPerDay <= 0) return;

        var now = DateTime.UtcNow;
        var oneMinuteAgo = now.AddMinutes(-1);
        var oneDayAgo = now.AddDays(-1);

        var query = _dbContext.ai_usage_logs
            .Where(x => x.user_id == userId && x.action_type == ActionSuggestNextChapter);

        if (maxPerMinute > 0)
        {
            var countMinute = query.Count(x => x.created_at >= oneMinuteAgo);
            if (countMinute >= maxPerMinute)
                throw new RateLimitExceededException(
                    $"Bạn chỉ được gọi gợi ý chương tiếp tối đa {maxPerMinute} lần/phút. Vui lòng thử lại sau 1 phút.");
        }

        if (maxPerDay > 0)
        {
            var countDay = query.Count(x => x.created_at >= oneDayAgo);
            if (countDay >= maxPerDay)
                throw new RateLimitExceededException(
                    $"Bạn đã đạt giới hạn {maxPerDay} lần gọi gợi ý/ngày. Vui lòng thử lại vào ngày mai.");
        }
    }

    private (string SystemMessage, string UserMessage) BuildSuggestNextChapterPrompt(stories story, List<chapters> recentChapters)
    {
        var systemMessage = """
Bạn là trợ lý sáng tác cho một nền tảng viết truyện. Nhiệm vụ: đọc ngữ cảnh truyện (tóm tắt + nội dung các chương gần nhất) và đưa ra 3–5 GỢI Ý cho chương tiếp theo.
- Mỗi gợi ý chỉ là outline (hướng phát triển), KHÔNG viết full nội dung.
- Giữ nhất quán phong cách, nhân vật và cốt truyện.
- Trả về đúng JSON sau, không thêm markdown hay giải thích ngoài JSON:
{"suggestions":[{"title":"Tiêu đề gợi ý 1","description":"Mô tả ngắn hướng phát triển"},{"title":"Tiêu đề gợi ý 2","description":"Mô tả ngắn"}, ...]}
""";

        var sb = new StringBuilder();
        sb.AppendLine("## Thông tin truyện");
        sb.AppendLine("- Tựa đề: " + (story.title ?? ""));
        sb.AppendLine("- Tóm tắt: " + (story.summary ?? "(không có)"));
        sb.AppendLine();
        sb.AppendLine("## Nội dung các chương gần nhất");
        foreach (var ch in recentChapters)
        {
            var content = ch.content ?? "";
            if (content.Length > MaxCharsPerChapter)
                content = content[..MaxCharsPerChapter] + "...";
            sb.AppendLine($"### Chương {ch.order_index}: {ch.title}");
            sb.AppendLine(content);
            sb.AppendLine();
        }
        sb.AppendLine("## Yêu cầu");
        sb.AppendLine("Dựa trên ngữ cảnh trên, đưa ra 3–5 gợi ý (title + description) cho chương tiếp theo. Trả về JSON với key \"suggestions\" là mảng các object có \"title\" và \"description\".");

        return (systemMessage, sb.ToString());
    }

    private const int DefaultMaxRetriesOn429 = 0;
    private const int DefaultRetryDelayBaseMs = 20000;
    private const int DefaultMinSecondsBetweenCalls = 60;

    private static readonly SemaphoreSlim GeminiThrottle = new(1);
    private static DateTimeOffset _lastGeminiCallUtc = DateTimeOffset.MinValue;

    private async Task<(string Content, string? ModelUsed, int? PromptTokens, int? CompletionTokens)> CallGeminiGenerateContentAsync(
        string apiKey,
        string systemMessage,
        string userMessage,
        CancellationToken cancellationToken)
    {
        await GeminiThrottle.WaitAsync(cancellationToken);
        try
        {
            var minSeconds = _configuration.GetValue("Gemini:MinSecondsBetweenCalls", DefaultMinSecondsBetweenCalls);
            var elapsed = (DateTimeOffset.UtcNow - _lastGeminiCallUtc).TotalSeconds;
            if (elapsed < minSeconds && _lastGeminiCallUtc != DateTimeOffset.MinValue)
            {
                var waitMs = (int)Math.Ceiling((minSeconds - elapsed) * 1000);
                if (waitMs > 0)
                {
                    _logger.LogInformation("Gemini throttle: đợi {WaitMs}ms trước lần gọi tiếp (tránh vượt quota).", waitMs);
                    await Task.Delay(waitMs, cancellationToken);
                }
            }
            _lastGeminiCallUtc = DateTimeOffset.UtcNow;

            return await CallGeminiInternalAsync(apiKey, systemMessage, userMessage, cancellationToken);
        }
        finally
        {
            GeminiThrottle.Release();
        }
    }

    private async Task<(string Content, string? ModelUsed, int? PromptTokens, int? CompletionTokens)> CallGeminiInternalAsync(
        string apiKey,
        string systemMessage,
        string userMessage,
        CancellationToken cancellationToken)
    {
        var model = _configuration["Gemini:Model"] ?? _configuration["Ai:Gemini:Model"] ?? DefaultModel;
        var url = $"{GeminiBaseUrl}/{model}:generateContent?key={Uri.EscapeDataString(apiKey)}";

        var requestBody = new
        {
            systemInstruction = new { parts = new[] { new { text = systemMessage } } },
            contents = new[] { new { parts = new[] { new { text = userMessage } } } },
            generationConfig = new { maxOutputTokens = 1024, temperature = 0.7 }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var client = _httpClientFactory.CreateClient();
        string responseJson = "";
        HttpResponseMessage? response = null;

        var maxRetries = _configuration.GetValue("Gemini:MaxRetriesOn429", DefaultMaxRetriesOn429);
        var delayBaseMs = _configuration.GetValue("Gemini:RetryDelayBaseMs", DefaultRetryDelayBaseMs);

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            response = await client.SendAsync(request, cancellationToken);
            responseJson = await response.Content.ReadAsStringAsync(cancellationToken);

            if (response.IsSuccessStatusCode)
                break;

            if ((int)response.StatusCode == 429 && attempt < maxRetries)
            {
                var delayMs = delayBaseMs * (1 << (attempt - 1));
                if (delayMs > 60_000) delayMs = 60_000;
                _logger.LogWarning("Gemini 429 Too Many Requests, retry {Attempt}/{Max} after {Delay}ms.", attempt, maxRetries, delayMs);
                await Task.Delay(delayMs, cancellationToken);
                continue;
            }

            _logger.LogWarning("Gemini API error {StatusCode}: {Response}", response.StatusCode, responseJson.Length > 500 ? responseJson[..500] : responseJson);
            throw new HttpRequestException($"Gemini API returned {(int)response.StatusCode}: {response.ReasonPhrase}. {responseJson}");
        }

        using var doc = JsonDocument.Parse(responseJson);
        var root = doc.RootElement;

        var content = "";
        if (root.TryGetProperty("candidates", out var candidates) && candidates.GetArrayLength() > 0)
        {
            var c0 = candidates[0];
            if (c0.TryGetProperty("content", out var contentObj) && contentObj.TryGetProperty("parts", out var parts) && parts.GetArrayLength() > 0)
                content = parts[0].TryGetProperty("text", out var text) ? text.GetString() ?? "" : "";
        }

        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            if (usage.TryGetProperty("promptTokenCount", out var pt))
                promptTokens = pt.GetInt32();
            if (usage.TryGetProperty("candidatesTokenCount", out var ct))
                completionTokens = ct.GetInt32();
        }

        return (content, model, promptTokens, completionTokens);
    }

    private static List<NextChapterSuggestionItemDto> ParseSuggestionsFromJson(string content)
    {
        var list = new List<NextChapterSuggestionItemDto>();
        content = content.Trim();
        if (content.StartsWith("```"))
        {
            var first = content.IndexOf('\n');
            if (first > 0)
                content = content[(first + 1)..];
            var last = content.LastIndexOf("```", StringComparison.Ordinal);
            if (last >= 0)
                content = content[..last];
        }

        try
        {
            using var doc = JsonDocument.Parse(content);
            var root = doc.RootElement;
            if (root.TryGetProperty("suggestions", out var arr))
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var title = item.TryGetProperty("title", out var t) ? t.GetString() ?? "" : "";
                    var description = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    list.Add(new NextChapterSuggestionItemDto { Title = title, Description = description });
                }
            }
        }
        catch
        {
            // ignore parse error, return empty list
        }

        return list;
    }

    private void TryLogUsage(Guid? userId, Guid? storyId, Guid? chapterId, string actionType, string modelName, int promptTokens, int completionTokens, string status)
    {
        if (_dbContext == null) return;
        try
        {
            _dbContext.ai_usage_logs.Add(new ai_usage_logs
            {
                user_id = userId,
                story_id = storyId,
                chapter_id = chapterId,
                action_type = actionType,
                model_name = modelName,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                status = status,
                created_at = DateTime.UtcNow
            });
            _dbContext.SaveChanges();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to log AI usage.");
        }
    }
}
