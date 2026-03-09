using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>Guardrail nội dung: từ cấm (BannedWords).</summary>
public interface IContentGuardrailService
{
    /// <summary>Kiểm tra bản nháp: không chứa từ cấm (config BannedWords).</summary>
    Task<GuardrailResult> CheckAsync(Guid storyId, string draftContent, CancellationToken cancellationToken = default);
}
