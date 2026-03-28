using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Text;

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

    public Task<GuardrailResult> CheckAsync(Guid storyId, string draftContent, CancellationToken cancellationToken = default) =>
        CheckAgainstWordList(draftContent, GetBannedWords());

    public Task<GuardrailResult> CheckCommentBannedWordsAsync(string content, CancellationToken cancellationToken = default) =>
        CheckAgainstWordList(content, GetBannedWordsCommentOnly());

    private static Task<GuardrailResult> CheckAgainstWordList(string? draftContent, string[] bannedWords)
    {
        var violations = new List<GuardrailViolation>();
        var draft = (draftContent ?? "").Trim();
        if (draft.Length == 0)
            return Task.FromResult(new GuardrailResult { Passed = true, Violations = violations });

        // Chuẩn hóa unicode + chữ thường; GIỮ DẤU tiếng Việt để tránh false-positive
        // (vd: "cặc" không được match "các", "cách").
        var draftNorm = NormalizeForMatch(draft);

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
                    Quote = w
                });
        }

        return Task.FromResult(new GuardrailResult
        {
            Passed = violations.Count == 0,
            Violations = violations
        });
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
}
