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
        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

        var titlePart = string.IsNullOrWhiteSpace(request.ChapterTitle) ? "" : $"Tiêu đề chương: {request.ChapterTitle}\n\n";
        var userPrompt = $@"{titlePart}Nội dung chương cần kiểm tra chính tả:

---
{content}
---

Nhiệm vụ: CHỈ tìm lỗi chính tả/đánh máy (typo) trong đoạn trên (tiếng Việt hoặc tiếng Anh).

RÀNG BUỘC BẮT BUỘC:
- Chỉ ghi nhận khi chắc chắn là typo. Nếu không chắc chắn: bỏ qua.
- Tuyệt đối không gợi ý thay đổi văn phong, ngữ nghĩa, đại từ, hoặc “trau chuốt” câu chữ.
- Không paraphrase, không biên tập, không thay từ đúng bằng từ khác.
- Không bịa lỗi.

Trả về DUY NHẤT một JSON hợp lệ, không markdown hay giải thích:
{{ ""spellingErrors"": [ {{ ""wordOrPhrase"": ""từ/cụm sai"", ""suggestion"": ""gợi ý sửa"", ""context"": ""câu chứa lỗi (tùy chọn)"" }} ], ""summary"": ""Tóm tắt ngắn cho tác giả (1-2 câu)"" }}

Nếu không có lỗi chính tả (hoặc không chắc chắn): spellingErrors = [], summary = ""Không phát hiện lỗi chính tả.""";

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
Bạn là hệ thống kiểm tra chính tả (typo) cho nội dung chương truyện.

CHỈ được phép trả về các lỗi chính tả/đánh máy khi chắc chắn. Nếu không chắc chắn thì phải bỏ qua và trả danh sách rỗng.
TUYỆT ĐỐI CẤM: đổi văn phong, đổi ngữ nghĩa, đổi đại từ, biên tập câu, diễn giải lại, hoặc thay từ đúng bằng từ khác.

Đầu ra BẮT BUỘC: chỉ một JSON hợp lệ theo đúng schema:
{ "spellingErrors": [ { "wordOrPhrase": "...", "suggestion": "...", "context": "..." } ], "summary": "..." }
Không markdown. Không thêm text ngoài JSON.
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
                    var wordOrPhrase = item.TryGetProperty("wordOrPhrase", out var w) ? w.GetString() ?? "" : "";
                    var suggestion = item.TryGetProperty("suggestion", out var s) ? s.GetString() ?? "" : "";
                    var context = item.TryGetProperty("context", out var c) ? c.GetString() : null;

                    if (!IsLikelyTypoCorrection(wordOrPhrase, suggestion))
                        continue;

                    spelling.Add(new SpellingIssue
                    {
                        WordOrPhrase = wordOrPhrase,
                        Suggestion = suggestion,
                        Context = context
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

    private static bool IsLikelyTypoCorrection(string wordOrPhrase, string suggestion)
    {
        wordOrPhrase = (wordOrPhrase ?? "").Trim();
        suggestion = (suggestion ?? "").Trim();
        if (wordOrPhrase.Length == 0 || suggestion.Length == 0) return false;
        if (string.Equals(wordOrPhrase, suggestion, StringComparison.OrdinalIgnoreCase)) return false;

        // Chỉ chấp nhận "sửa typo": không được thêm/bớt số từ (tránh kiểu biên tập/diễn đạt lại).
        var wc1 = CountWords(wordOrPhrase);
        var wc2 = CountWords(suggestion);
        if (wc1 == 0 || wc2 == 0) return false;
        if (wc1 != wc2) return false;

        // Heuristic: độ khác biệt nhỏ (đánh máy/sai dấu). Từ quá dài khác hẳn thường là biên tập.
        var len1 = wordOrPhrase.Length;
        var len2 = suggestion.Length;
        if (Math.Abs(len1 - len2) > 3) return false;

        var dist = LevenshteinDistance(
            wordOrPhrase.ToLowerInvariant(),
            suggestion.ToLowerInvariant(),
            maxDistance: 3);
        return dist >= 1 && dist <= 3;
    }

    private static int CountWords(string s)
        => s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static int LevenshteinDistance(string a, string b, int maxDistance)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (Math.Abs(a.Length - b.Length) > maxDistance) return maxDistance + 1;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            var minInRow = curr[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
                if (curr[j] < minInRow) minInRow = curr[j];
            }

            if (minInRow > maxDistance) return maxDistance + 1;
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
