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

/// <summary>Kiểm tra bản nháp chương có nhất quán với cốt truyện; dùng Story memory (N chương gần nhất) làm ngữ cảnh.</summary>
public class AIConsistencyCheckService : IAIConsistencyCheckService
{
    private const string ActionConsistencyCheck = "CONSISTENCY_CHECK";

    private readonly IStoryRepository _storyRepository;
    private readonly IStoryContextBuilder _contextBuilder;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IConfiguration _configuration;

    public AIConsistencyCheckService(
        IStoryRepository storyRepository,
        IStoryContextBuilder contextBuilder,
        IAIUsageLogRepository aiUsageLogRepository,
        IConfiguration configuration)
    {
        _storyRepository = storyRepository;
        _contextBuilder = contextBuilder;
        _aiUsageLogRepository = aiUsageLogRepository;
        _configuration = configuration;
    }

    public async Task<ConsistencyCheckResponse> CheckConsistencyAsync(
        ConsistencyCheckRequest request,
        Guid authorUserId,
        CancellationToken cancellationToken = default)
    {
        var story = _storyRepository.GetById(request.StoryId);
        if (story == null)
            throw new InvalidOperationException("Truyện không tồn tại.");

        if (story.author_id != authorUserId)
            throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được sử dụng tính năng kiểm tra nhất quán.");

        if (string.IsNullOrWhiteSpace(request.DraftContent))
            throw new InvalidOperationException("DraftContent (nội dung bản nháp) là bắt buộc.");

        var contextBlock = _contextBuilder.BuildForCheckConsistency(request.StoryId, request.DraftContent, request.AfterChapterId, request.ChapterTitle);
        if (string.IsNullOrWhiteSpace(contextBlock))
            throw new InvalidOperationException("Truyện cần có ít nhất một chương đã có nội dung để kiểm tra nhất quán.");

        var storyLanguage = StoryLanguageHelper.DetectFromStoryContext(contextBlock);
        var languageInstruction = StoryLanguageHelper.GetLanguageInstruction(storyLanguage);

        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfig(_configuration);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

        var userPrompt = $@"{contextBlock}

---
{languageInstruction}

Nhiệm vụ: Kiểm tra xem bản nháp (phần cuối ngữ cảnh trên) có MÂU THUẪN với cốt truyện trong ngữ cảnh không. Ví dụ: nhân vật đã chết hoặc đã rời đi nhưng lại xuất hiện; sự kiện đã xảy ra nhưng bản nháp mô tả ngược lại; địa điểm/ thời gian không khớp. Chỉ báo lỗi khi có bằng chứng rõ ràng từ ngữ cảnh.

Trả về DUY NHẤT một JSON hợp lệ, không kèm markdown hay giải thích:
{{ ""hasIssues"": true/false, ""issues"": [ {{ ""type"": ""character""|""event""|""timeline""|""location""|""other"", ""description"": ""Mô tả ngắn gọn cho tác giả"", ""referenceChapter"": số chương tham chiếu (nếu có) }} ] }}

Nếu không có mâu thuẫn, trả về: {{ ""hasIssues"": false, ""issues"": [] }}";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt()),
            new UserChatMessage(userPrompt)
        };

        var completion = await client.CompleteChatAsync(messages);
        var chat = completion.Value;
        var text = chat.Content?.Count > 0 ? chat.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            LogUsage(authorUserId, request.StoryId, null, ActionConsistencyCheck, model, 0, 0);
            return new ConsistencyCheckResponse { HasIssues = false, Issues = new List<ConsistencyIssue>() };
        }

        LogUsage(authorUserId, request.StoryId, null, ActionConsistencyCheck, model, 0, 0);
        return ParseConsistencyResult(text);
    }

    private static string GetSystemPrompt()
    {
        return """
Bạn là trợ lý kiểm tra tính nhất quán của truyện. Nhiệm vụ: đọc ngữ cảnh (nội dung 5 chương gần nhất) và bản nháp chương mới; phát hiện MÂU THUẪN rõ ràng — ví dụ nhân vật đã chết/ mất tích nhưng lại xuất hiện, sự kiện đã xảy ra nhưng bản nháp mô tả ngược lại, timeline/ địa điểm sai. Chỉ báo lỗi khi chắc chắn có mâu thuẫn với ngữ cảnh; không suy diễn quá xa. Trả về đúng JSON theo cấu trúc đã nêu, ngôn ngữ mô tả trùng với truyện (Việt hoặc Anh).
""";
    }

    private static ConsistencyCheckResponse ParseConsistencyResult(string text)
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
            var hasIssues = root.TryGetProperty("hasIssues", out var h) && h.GetBoolean();
            var issues = new List<ConsistencyIssue>();
            if (root.TryGetProperty("issues", out var arr) && arr.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in arr.EnumerateArray())
                {
                    var type = item.TryGetProperty("type", out var t) ? t.GetString() ?? "other" : "other";
                    var description = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                    int? refCh = null;
                    if (item.TryGetProperty("referenceChapter", out var r) && r.ValueKind == JsonValueKind.Number && r.TryGetInt32(out var n))
                        refCh = n;
                    issues.Add(new ConsistencyIssue { Type = type, Description = description, ReferenceChapter = refCh });
                }
            }

            return new ConsistencyCheckResponse { HasIssues = hasIssues, Issues = issues };
        }
        catch
        {
            return new ConsistencyCheckResponse { HasIssues = false, Issues = new List<ConsistencyIssue>() };
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
