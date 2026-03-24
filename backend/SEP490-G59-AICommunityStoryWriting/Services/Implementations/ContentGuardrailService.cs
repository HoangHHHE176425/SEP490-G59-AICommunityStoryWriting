using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Interfaces;
using System.Globalization;
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

        // Bỏ dấu + chữ thường để khớp keyword Anh/Việt ổn định.
        var draftNorm = NormalizeForMatch(draft);

        foreach (var word in bannedWords)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;
            var w = word.Trim();
            var wNorm = NormalizeForMatch(w);
            if (wNorm.Length == 0) continue;
            if (draftNorm.Contains(wNorm, StringComparison.Ordinal))
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

        // Remove diacritics by decomposing to FormD and stripping non-spacing marks.
        var formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc == UnicodeCategory.NonSpacingMark) continue;
            sb.Append(ch);
        }

        // Normalize back to FormC rồi chữ thường để so khớp không phân hoa/thường.
        return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
