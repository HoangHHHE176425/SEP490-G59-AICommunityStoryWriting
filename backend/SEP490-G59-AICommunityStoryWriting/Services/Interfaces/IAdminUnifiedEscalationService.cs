using Services.DTOs.Admin;

namespace Services.Interfaces;

public interface IAdminUnifiedEscalationService
{
    Task<AdminUnifiedEscalationPendingResponseDto> GetPendingUnifiedAsync(string? urgencyTierFilter);

    Task<PagedResultDto<UnifiedEscalationLogItemDto>> SearchUnifiedLogAsync(UnifiedEscalationLogQueryDto query);
}
