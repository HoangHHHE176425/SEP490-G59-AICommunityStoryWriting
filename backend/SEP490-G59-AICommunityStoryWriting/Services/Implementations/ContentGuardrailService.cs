using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;
using System.Text.Json;

namespace Services.Implementations;

/// <summary>Guardrail nội dung: từ cấm (BannedWords). Ưu tiên đọc từ DB (ai_sensitive_words, category = BannedWord); không có thì dùng config.</summary>
public class ContentGuardrailService : IContentGuardrailService
{
    /// <summary>Category dùng cho từ cấm check-chapter (admin quản lý qua API).</summary>
    public const string BannedWordCategory = "BannedWord";

    private readonly IConfiguration _configuration;
    private readonly IAiSensitiveWordsRepository _bannedWordsRepository;

    public ContentGuardrailService(IConfiguration configuration, IAiSensitiveWordsRepository bannedWordsRepository)
    {
        _configuration = configuration;
        _bannedWordsRepository = bannedWordsRepository;
    }

    public async Task<GuardrailResult> CheckAsync(Guid storyId, string draftContent, CancellationToken cancellationToken = default)
    {
        var bannedWords = GetBannedWords();
        var ruleResult = CheckAgainstWordList(draftContent, bannedWords);
        if (!_configuration.GetValue("ContentGuardrail:EnableAiBannedWords", true))
            return ruleResult;
        return await MergeWithAiResultAsync(draftContent, bannedWords, ruleResult, cancellationToken);
    }

    public async Task<GuardrailResult> CheckCommentBannedWordsAsync(string content, CancellationToken cancellationToken = default)
    {
        var bannedWords = GetBannedWordsCommentOnly();
        var ruleResult = CheckAgainstWordList(content, bannedWords);
        if (!_configuration.GetValue("ContentGuardrail:EnableAiBannedWords", true))
            return ruleResult;
        return await MergeWithAiResultAsync(content, bannedWords, ruleResult, cancellationToken);
    }

    private static GuardrailResult CheckAgainstWordList(string? draftContent, string[] bannedWords)
    {
        var violations = new List<GuardrailViolation>();
        var draft = (draftContent ?? "").Trim();
        if (draft.Length == 0)
            return new GuardrailResult { Passed = true, Violations = violations };

        // Chuẩn hóa unicode + chữ thường; GIỮ DẤU tiếng Việt để tránh false-positive
        // (vd: "cặc" không được match "các", "cách").
        var draftSource = draft.Normalize(NormalizationForm.FormC);
        var draftNorm = NormalizeForMatch(draftSource);

        foreach (var word in bannedWords)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;
            var w = word.Trim();
            var wNorm = NormalizeForMatch(w);
            if (wNorm.Length == 0) continue;
            if (ContainsWholeWord(draftNorm, wNorm))
                violations.Add(new GuardrailViolation
                {
                    Type = "BannedWord",
                    Message = "Nội dung chứa từ không được phép.",
                    Quote = ExtractContextSnippet(draftSource, draftNorm, wNorm)
                });
        }

        return new GuardrailResult
        {
            Passed = violations.Count == 0,
            Violations = violations
        };
    }

    private async Task<GuardrailResult> MergeWithAiResultAsync(
        string? draftContent,
        string[] bannedWords,
        GuardrailResult ruleResult,
        CancellationToken cancellationToken)
    {
        var aiResult = await DetectWithAiAsync(draftContent, bannedWords, cancellationToken);
        if (aiResult == null || aiResult.Violations.Count == 0)
            return ruleResult;

        var merged = new List<GuardrailViolation>(ruleResult.Violations);
        var set = new HashSet<string>(
            merged.Select(v => $"{v.Type}|{v.Quote}".ToLowerInvariant()),
            StringComparer.OrdinalIgnoreCase);

        foreach (var v in aiResult.Violations)
        {
            var key = $"{v.Type}|{v.Quote}".ToLowerInvariant();
            if (set.Add(key))
                merged.Add(v);
        }

        return new GuardrailResult { Passed = merged.Count == 0, Violations = merged };
    }

    private async Task<GuardrailResult?> DetectWithAiAsync(
        string? draftContent,
        string[] bannedWords,
        CancellationToken cancellationToken)
    {
        var normalized = ChapterContentNormalizer.NormalizeForAi(draftContent, 6000);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        try
        {
            var (provider, model, apiKey, baseUrl) =
                AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);
            var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);
            var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentConsistencyChecker)
                          ?? new ChatCompletionOptions();
            options.Temperature = 0;
            options.TopP = 1;

            var bannedList = string.Join(", ", bannedWords.Where(x => !string.IsNullOrWhiteSpace(x)).Take(300));
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("""
Bạn là bộ lọc từ cấm theo ngữ cảnh cho nội dung truyện.
Chỉ trả về JSON hợp lệ, không markdown, không giải thích ngoài JSON.
Nếu không phát hiện vi phạm thì trả {"violations":[]}.
Mỗi violation phải có:
- matchedPhrase: từ/cụm vi phạm.
- contextSnippet: đoạn trích chứa matchedPhrase, chỉ tối đa 24 ký tự trước và 24 ký tự sau.
- reason: lý do ngắn.
"""),
                new UserChatMessage($"""
Danh sách từ cấm tham chiếu: {bannedList}

Nội dung cần kiểm tra:
---
{normalized}
---

Trả về JSON object có field "violations" là mảng object gồm: matchedPhrase, contextSnippet, reason.
""")
            };

            var completion = await client.CompleteChatAsync(messages, options, cancellationToken);
            var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
            if (string.IsNullOrWhiteSpace(text))
                return null;
            return ParseAiGuardrail(text, normalized);
        }
        catch
        {
            // Nếu AI lỗi/time-out thì fallback kết quả rule-based, không làm fail request.
            return null;
        }
    }

    private static GuardrailResult ParseAiGuardrail(string raw, string source)
    {
        var t = UnwrapJson(raw);
        using var doc = JsonDocument.Parse(t);
        var root = doc.RootElement;
        var list = new List<GuardrailViolation>();
        if (!root.TryGetProperty("violations", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return new GuardrailResult { Passed = true, Violations = list };

        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            var phrase = item.TryGetProperty("matchedPhrase", out var p) ? p.GetString() ?? "" : "";
            var snippet = item.TryGetProperty("contextSnippet", out var c) ? c.GetString() ?? "" : "";
            var reason = item.TryGetProperty("reason", out var r) ? r.GetString() ?? "" : "";
            snippet = ClampAroundNeedle(snippet, phrase, source);
            if (string.IsNullOrWhiteSpace(snippet) && !string.IsNullOrWhiteSpace(phrase))
                snippet = ExtractContextSnippet(source, NormalizeForMatch(source), NormalizeForMatch(phrase), 24);
            if (string.IsNullOrWhiteSpace(snippet)) continue;

            list.Add(new GuardrailViolation
            {
                Type = "BannedWord",
                Message = string.IsNullOrWhiteSpace(reason) ? "Nội dung chứa từ không được phép." : reason.Trim(),
                Quote = snippet
            });
        }

        return new GuardrailResult { Passed = list.Count == 0, Violations = list };
    }

    private static string UnwrapJson(string raw)
    {
        var t = raw.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var start = t.IndexOf('\n');
            if (start >= 0)
            {
                start++;
                var end = t.IndexOf("```", start, StringComparison.Ordinal);
                if (end > start) t = t[start..end].Trim();
            }
        }
        var i = t.IndexOf('{');
        var j = t.LastIndexOf('}');
        if (i >= 0 && j > i) return t[i..(j + 1)];
        return t;
    }

    private static string ClampAroundNeedle(string? snippet, string? needle, string source)
    {
        var s = (snippet ?? "").Trim();
        var n = (needle ?? "").Trim();
        if (s.Length == 0 || n.Length == 0) return s;

        var idx = s.IndexOf(n, StringComparison.OrdinalIgnoreCase);
        if (idx >= 0)
        {
            const int ctx = 24;
            var start = Math.Max(0, idx - ctx);
            var end = Math.Min(s.Length, idx + n.Length + ctx);
            return (start > 0 ? "..." : "") + s[start..end].Trim() + (end < s.Length ? "..." : "");
        }

        var sourceSnippet = ExtractContextSnippet(source, NormalizeForMatch(source), NormalizeForMatch(n), 24);
        return sourceSnippet;
    }

    /// <summary>Lấy danh sách từ cấm: ưu tiên DB (ai_sensitive_words, category BannedWord), không có thì dùng config.</summary>
    private string[] GetBannedWords()
    {
        var fromDbBannedCategory = _bannedWordsRepository.GetAll(BannedWordCategory)
            .Select(w => w.word?.Trim())
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w!)
            .ToArray();

        // Backward/compat: nếu DB không có category "BannedWord" (ví dụ người ta dùng "violence"),
        // thì fallback lấy toàn bộ từ nhạy cảm trong bảng để đảm bảo vẫn chặn comment.
        if (fromDbBannedCategory.Length > 0) return fromDbBannedCategory;

        var fromDbAllCategories = _bannedWordsRepository.GetAll(null)
            .Select(w => w.word?.Trim())
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w!)
            .ToArray();
        if (fromDbAllCategories.Length > 0) return fromDbAllCategories;

        var fromConfig = ParseCommaSeparated(_configuration["ContentGuardrail:BannedWords"] ?? _configuration["AI:CoCreateBannedWords"]);
        return fromConfig;
    }

    /// <summary>Comment story/chapter: chỉ category BannedWord; không gộp mọi category trong bảng.</summary>
    private string[] GetBannedWordsCommentOnly()
    {
        var fromDb = _bannedWordsRepository.GetAll(BannedWordCategory)
            .Select(w => w.word?.Trim())
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w!)
            .ToArray();
        if (fromDb.Length > 0) return fromDb;
        return ParseCommaSeparated(_configuration["ContentGuardrail:BannedWords"] ?? _configuration["AI:CoCreateBannedWords"]);
    }

    private static string[] ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }

    private static string NormalizeForMatch(string input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;
        return input.Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }

    private static bool ContainsWholeWord(string textNormalized, string bannedWordNormalized)
    {
        if (string.IsNullOrWhiteSpace(textNormalized) || string.IsNullOrWhiteSpace(bannedWordNormalized))
            return false;
        var escaped = Regex.Escape(bannedWordNormalized);
        // Chỉ match theo biên từ để tránh dính chuỗi con.
        // \p{L}\p{N}: chữ/số Unicode, hỗ trợ tiếng Việt có dấu.
        var pattern = $@"(?<![\p{{L}}\p{{N}}_]){escaped}(?![\p{{L}}\p{{N}}_])";
        return Regex.IsMatch(textNormalized, pattern, RegexOptions.CultureInvariant);
    }

    private static string ExtractContextSnippet(string draftSource, string draftNormalized, string bannedWordNormalized, int contextChars = 24)
    {
        if (string.IsNullOrWhiteSpace(draftSource) || string.IsNullOrWhiteSpace(draftNormalized) || string.IsNullOrWhiteSpace(bannedWordNormalized))
            return bannedWordNormalized;

        var escaped = Regex.Escape(bannedWordNormalized);
        var pattern = $@"(?<![\p{{L}}\p{{N}}_]){escaped}(?![\p{{L}}\p{{N}}_])";
        var m = Regex.Match(draftNormalized, pattern, RegexOptions.CultureInvariant);
        if (!m.Success)
            return bannedWordNormalized;

        var start = Math.Max(0, m.Index - contextChars);
        var end = Math.Min(draftSource.Length, m.Index + m.Length + contextChars);
        var snippet = draftSource[start..end].Trim();
        if (start > 0) snippet = "..." + snippet;
        if (end < draftSource.Length) snippet += "...";
        return snippet;
    }
}
