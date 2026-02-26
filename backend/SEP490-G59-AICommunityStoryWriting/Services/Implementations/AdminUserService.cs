using BusinessObjects.Entities;
using DataAccessObjects.Queries;
using Repositories.Interfaces;
using Services.DTOs.Admin;
using Services.DTOs.Admin.Users;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AdminUserService : IAdminUserService
    {
        private readonly IUserRepository _userRepo;

        public AdminUserService(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task<PagedResultDto<AdminUserListItemDto>> GetUsersAsync(AdminUserQueryDto query)
        {
            var daoQuery = new AdminUserQuery
            {
                Search = query.Search,
                Role = query.Role,
                Status = query.Status,
                Page = query.Page,
                PageSize = query.PageSize,
                SortBy = query.SortBy,
                SortOrder = query.SortOrder
            };

            var (items, total) = await _userRepo.GetUsersAsync(daoQuery);
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

            return new PagedResultDto<AdminUserListItemDto>
            {
                Items = items.Select(MapListItem),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AdminUserDetailDto?> GetUserByIdAsync(Guid id)
        {
            var u = await _userRepo.GetUserById(id);
            return u == null ? null : MapDetail(u);
        }

        public async Task<AdminUserDetailDto> CreateAsync(AdminCreateUserRequest request)
        {
            var email = request.Email.Trim();
            if (await _userRepo.IsEmailExist(email))
            {
                throw new InvalidOperationException("Email already exists.");
            }

            var userId = Guid.NewGuid();
            var nickname = string.IsNullOrWhiteSpace(request.Nickname)
                ? email.Split('@')[0]
                : request.Nickname.Trim();

            var user = new users
            {
                id = userId,
                email = email,
                password_hash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                role = (request.Role ?? "USER").Trim().ToUpperInvariant(),
                status = (request.Status ?? "ACTIVE").Trim().ToUpperInvariant(),
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow,
                user_profiles = new user_profiles
                {
                    user_id = userId,
                    nickname = nickname,
                    Phone = request.Phone,
                    IdNumber = request.IdNumber,
                    settings = "{\"allow_notif\":true}",
                    updated_at = DateTime.UtcNow
                }
            };

            await _userRepo.AddUser(user);

            var created = await _userRepo.GetUserById(userId);
            return MapDetail(created!);
        }

        public async Task<bool> SetStatusAsync(Guid id, string status)
        {
            var user = await _userRepo.GetUserById(id);
            if (user == null) return false;

            user.status = (status ?? "").Trim().ToUpperInvariant();
            user.updated_at = DateTime.UtcNow;
            await _userRepo.UpdateUser(user);
            return true;
        }

        public async Task<bool> SetRoleAsync(Guid id, string role)
        {
            var user = await _userRepo.GetUserById(id);
            if (user == null) return false;

            user.role = (role ?? "").Trim().ToUpperInvariant();
            user.updated_at = DateTime.UtcNow;
            await _userRepo.UpdateUser(user);
            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var user = await _userRepo.GetUserById(id);
            if (user == null) return false;

            await _userRepo.DeleteUser(id);
            return true;
        }

        private static AdminUserListItemDto MapListItem(users u)
        {
            return new AdminUserListItemDto
            {
                Id = u.id,
                Email = u.email,
                Role = u.role,
                Status = u.status,
                CreatedAt = u.created_at,
                Nickname = u.user_profiles?.nickname,
                Phone = u.user_profiles?.Phone,
                IdNumber = u.user_profiles?.IdNumber,
                IsEmailVerified = u.email_verified_at != null
            };
        }

        private static AdminUserDetailDto MapDetail(users u)
        {
            var dto = new AdminUserDetailDto
            {
                Id = u.id,
                Email = u.email,
                Role = u.role,
                Status = u.status,
                CreatedAt = u.created_at,
                UpdatedAt = u.updated_at,
                EmailVerifiedAt = u.email_verified_at,
                DeletionRequestedAt = u.deletion_requested_at,
                Nickname = u.user_profiles?.nickname,
                Phone = u.user_profiles?.Phone,
                IdNumber = u.user_profiles?.IdNumber,
                IsEmailVerified = u.email_verified_at != null
            };
            return dto;
        }
    }
}

