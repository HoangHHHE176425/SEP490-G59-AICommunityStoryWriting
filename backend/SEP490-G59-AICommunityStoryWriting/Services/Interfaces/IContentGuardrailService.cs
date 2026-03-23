using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>Guardrail nội dung: từ cấm (BannedWords).</summary>
public interface IContentGuardrailService
{
    /// <summary>Kiểm tra bản nháp: không chứa từ cấm (config BannedWords).</summary>
    Task<GuardrailResult> CheckAsync(Guid storyId, string draftContent, CancellationToken cancellationToken = default);

    /// <summary>Comment story/chapter: chỉ dùng từ trong <c>ai_sensitive_words</c> với category <c>BannedWord</c> (không fallback toàn bộ category khác); nếu DB trống thì dùng config.</summary>
    Task<GuardrailResult> CheckCommentBannedWordsAsync(string content, CancellationToken cancellationToken = default);
}
