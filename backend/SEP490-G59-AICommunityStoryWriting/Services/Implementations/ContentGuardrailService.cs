using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Interfaces;

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

    public Task<GuardrailResult> CheckAsync(Guid storyId, string draftContent, CancellationToken cancellationToken = default)
    {
        var violations = new List<GuardrailViolation>();
        var draft = (draftContent ?? "").Trim();
        if (draft.Length == 0)
            return Task.FromResult(new GuardrailResult { Passed = true, Violations = violations });

        var bannedWords = GetBannedWords();

        foreach (var word in bannedWords)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;
            var w = word.Trim();
            if (draft.IndexOf(w, StringComparison.OrdinalIgnoreCase) >= 0)
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
        var fromDb = _bannedWordsRepository.GetAll(BannedWordCategory)
            .Select(w => w.word?.Trim())
            .Where(w => !string.IsNullOrEmpty(w))
            .Select(w => w!)
            .ToArray();
        if (fromDb.Length > 0) return fromDb;

        var fromConfig = ParseCommaSeparated(_configuration["ContentGuardrail:BannedWords"] ?? _configuration["AI:CoCreateBannedWords"]);
        return fromConfig;
    }

    private static string[] ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }
}
