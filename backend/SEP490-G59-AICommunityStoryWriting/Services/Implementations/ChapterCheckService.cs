using System.Text.Json;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Kiểm tra chương: chính tả (AI) và từ cấm (BannedWords).</summary>
public class ChapterCheckService : IChapterCheckService
{
    private const string ActionChapterCheck = "CHAPTER_CHECK";

    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IConfiguration _configuration;
    private readonly IContentGuardrailService _guardrail;

    public ChapterCheckService(
        IAIUsageLogRepository aiUsageLogRepository,
        IConfiguration configuration,
        IContentGuardrailService guardrail)
    {
        _aiUsageLogRepository = aiUsageLogRepository;
        _configuration = configuration;
        _guardrail = guardrail;
    }

    public async Task<CheckChapterResponse> CheckAsync(CheckChapterRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return new CheckChapterResponse { Passed = true, Summary = "Nội dung trống, không cần kiểm tra." };

        var content = request.Content.Trim();
        if (content.Length > 50000)
            content = content[..50000] + "\n[... nội dung bị cắt bớt ...]";

        // 1) Kiểm tra từ cấm (BannedWords) – config ContentGuardrail:BannedWords / AI:CoCreateBannedWords
        var storyId = request.StoryId ?? Guid.Empty;
        var guardrailResult = await _guardrail.CheckAsync(storyId, content, cancellationToken);
        var policyViolations = new List<PolicyViolationItem>();
        foreach (var v in guardrailResult.Violations)
            policyViolations.Add(new PolicyViolationItem { Type = v.Type, Description = v.Message, Quote = v.Quote });

        // 2) Kiểm tra chính tả bằng AI
        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfig(_configuration);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

        var titlePart = string.IsNullOrWhiteSpace(request.ChapterTitle) ? "" : $"Tiêu đề chương: {request.ChapterTitle}\n\n";
        var userPrompt = $@"{titlePart}Nội dung chương cần kiểm tra chính tả:

---
{content}
---

Nhiệm vụ: Tìm lỗi chính tả (tiếng Việt hoặc tiếng Anh) trong đoạn trên. Với mỗi lỗi, đưa ra từ/cụm sai và gợi ý sửa. Bỏ qua nếu không có lỗi.

Trả về DUY NHẤT một JSON hợp lệ, không markdown hay giải thích:
{{ ""spellingErrors"": [ {{ ""wordOrPhrase"": ""từ/cụm sai"", ""suggestion"": ""gợi ý sửa"", ""context"": ""câu chứa lỗi (tùy chọn)"" }} ], ""summary"": ""Tóm tắt ngắn cho tác giả (1-2 câu)"" }}

Nếu không có lỗi chính tả: spellingErrors = [], summary = ""Không phát hiện lỗi chính tả.""";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt()),
            new UserChatMessage(userPrompt)
        };

        var options = AIClientHelper.GetCompletionOptions(_configuration, null);
        var completion = await client.CompleteChatAsync(messages, options);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;

        if (userId.HasValue)
        {
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = userId.Value,
                story_id = request.StoryId,
                chapter_id = null,
                action_type = ActionChapterCheck,
                model_name = model,
                prompt_tokens = 0,
                completion_tokens = 0,
                total_tokens = 0,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });
        }

        if (string.IsNullOrWhiteSpace(text))
            return new CheckChapterResponse
            {
                Passed = policyViolations.Count == 0,
                PolicyViolations = policyViolations,
                Summary = "Không đọc được kết quả kiểm tra chính tả từ AI."
            };

        var (spellingIssues, summary) = ParseSpellingResponse(text);
        var passed = spellingIssues.Count == 0 && policyViolations.Count == 0;
        return new CheckChapterResponse
        {
            Passed = passed,
            SpellingIssues = spellingIssues,
            PolicyViolations = policyViolations,
            HasInappropriateContent = false,
            Summary = summary
        };
    }

    private static string GetSystemPrompt()
    {
        return """
Bạn là trợ lý kiểm tra chính tả cho nội dung chương truyện. Nhiệm vụ: Phát hiện lỗi chính tả (tiếng Việt hoặc Anh) và gợi ý sửa. (Từ cấm/chính sách nội dung được hệ thống kiểm tra riêng từ DB; bạn chỉ tập trung vào chính tả.) Trả về đúng JSON theo cấu trúc đã nêu (spellingErrors, summary), ngôn ngữ mô tả trùng với nội dung (Việt hoặc Anh).
""";
    }

    private static (List<SpellingIssue> SpellingIssues, string? Summary) ParseSpellingResponse(string text)
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
            var spelling = new List<SpellingIssue>();
            if (root.TryGetProperty("spellingErrors", out var se) && se.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in se.EnumerateArray())
                {
                    spelling.Add(new SpellingIssue
                    {
                        WordOrPhrase = item.TryGetProperty("wordOrPhrase", out var w) ? w.GetString() ?? "" : "",
                        Suggestion = item.TryGetProperty("suggestion", out var s) ? s.GetString() ?? "" : "",
                        Context = item.TryGetProperty("context", out var c) ? c.GetString() : null
                    });
                }
            }
            var summary = root.TryGetProperty("summary", out var sum) ? sum.GetString() : null;
            return (spelling, summary);
        }
        catch
        {
            return (new List<SpellingIssue>(), "Định dạng phản hồi không hợp lệ.");
        }
    }
}
