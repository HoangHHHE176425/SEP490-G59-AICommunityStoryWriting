using Services.DTOs.Admin;
using Services.DTOs.Admin.Policies;

namespace Services.Interfaces
{
    public interface IAdminPolicyService
    {
        Task<PagedResultDto<AdminPolicyListItemDto>> GetPoliciesAsync(AdminPolicyQueryDto query);
        Task<AdminPolicyDetailDto?> GetPolicyByIdAsync(Guid id);
        Task<AdminPolicyDetailDto> CreateAsync(AdminCreatePolicyRequest request);
        Task<bool> UpdateAsync(Guid id, AdminUpdatePolicyRequest request);
        Task<bool> DeleteAsync(Guid id);
        Task<bool> ActivateAsync(Guid id);
        Task<bool> DeactivateAsync(Guid id);
    }
}

