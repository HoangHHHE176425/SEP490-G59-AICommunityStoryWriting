using Services.DTOs.Admin;
using Services.DTOs.Admin.Users;

namespace Services.Interfaces
{
    public interface IAdminUserService
    {
        Task<PagedResultDto<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query);
        Task<AdminUserDetailDto?> GetUserByIdAsync(Guid id);
        Task<AdminUserDetailDto> CreateAsync(AdminCreateUserRequest request);
        Task<bool> SetStatusAsync(Guid id, string status);
        Task<bool> SetRoleAsync(Guid id, string role);
        Task<bool> DeleteAsync(Guid id);
    }
}

