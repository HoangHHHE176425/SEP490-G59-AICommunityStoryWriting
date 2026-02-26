using BusinessObjects.Entities;
using DataAccessObjects.Queries;
using Repositories.Interfaces;
using Services.DTOs.Admin;
using Services.DTOs.Admin.Users;
using Services.Interfaces;
using System;

namespace Services.Implementations
{
    public class AdminUserService : IAdminUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IModeratorCategoryAssignmentRepository _modCatRepo;

        public AdminUserService(IUserRepository userRepo, IModeratorCategoryAssignmentRepository modCatRepo)
        {
            _userRepo = userRepo;
            _modCatRepo = modCatRepo;
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

        public async Task<AdminUserStatsDto> GetStatsAsync()
        {
            var (total, active, inactive, banned, pending, authors, moderators) = await _userRepo.GetStatsAsync();
            return new AdminUserStatsDto
            {
                Total = total,
                Active = active,
                Inactive = inactive,
                Banned = banned,
                Pending = pending,
                Authors = authors,
                Moderators = moderators
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
            // Ensure user exists (admin UX)
            return true;
        }

        public async Task<List<Guid>> GetModeratorCategoriesAsync(Guid userId)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new InvalidOperationException("User not found.");

            return await _modCatRepo.GetCategoryIdsAsync(userId);
        }

        public async Task<bool> SetModeratorCategoriesAsync(Guid userId, IReadOnlyCollection<Guid> categoryIds)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) return false;

            var ids = (categoryIds ?? Array.Empty<Guid>()).Distinct().ToList();
            await _modCatRepo.ReplaceAssignmentsAsync(userId, ids);

            // Keep user.role in sync for admin UX.
            var role = (user.role ?? "").Trim().ToUpperInvariant();
            if (ids.Count > 0)
            {
                if (role != "ADMIN" && role != "MODERATOR")
                {
                    user.role = "MODERATOR";
                    user.updated_at = DateTime.UtcNow;
                    await _userRepo.UpdateUser(user);
                }
            }
            else
            {
                if (role == "MODERATOR")
                {
                    user.role = "USER";
                    user.updated_at = DateTime.UtcNow;
                    await _userRepo.UpdateUser(user);
                }
            }

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
                EmailVerifiedAt = u.email_verified_at,
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

