using Microsoft.Extensions.Configuration;
using Services.DTOs.AI;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Guardrail nội dung: từ cấm (BannedWords).</summary>
public class ContentGuardrailService : IContentGuardrailService
{
    private readonly IConfiguration _configuration;

    public ContentGuardrailService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public Task<GuardrailResult> CheckAsync(Guid storyId, string draftContent, CancellationToken cancellationToken = default)
    {
        var violations = new List<GuardrailViolation>();
        var draft = (draftContent ?? "").Trim();
        if (draft.Length == 0)
            return Task.FromResult(new GuardrailResult { Passed = true, Violations = violations });

        var bannedWords = ParseCommaSeparated(_configuration["ContentGuardrail:BannedWords"] ?? _configuration["AI:CoCreateBannedWords"]);

        foreach (var word in bannedWords)
        {
            if (string.IsNullOrWhiteSpace(word)) continue;
            if (draft.IndexOf(word.Trim(), StringComparison.OrdinalIgnoreCase) >= 0)
                violations.Add(new GuardrailViolation
                {
                    Type = "BannedWord",
                    Message = "Nội dung chứa từ không được phép.",
                    Quote = word.Trim()
                });
        }

        return Task.FromResult(new GuardrailResult
        {
            Passed = violations.Count == 0,
            Violations = violations
        });
    }

    private static string[] ParseCommaSeparated(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Array.Empty<string>();
        return value.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
    }
}
