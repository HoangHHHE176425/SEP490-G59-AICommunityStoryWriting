using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using DataAccessObjects.Queries;
using Microsoft.EntityFrameworkCore;
using Repositories.Interfaces;
using Services.DTOs.Admin;
using Services.DTOs.Admin.Users;
using Services.Interfaces;
using System;
using Microsoft.Extensions.Configuration;

namespace Services.Implementations
{
    public class AdminUserService : IAdminUserService
    {
        private readonly IUserRepository _userRepo;
        private readonly IModeratorCategoryAssignmentRepository _modCatRepo;
        private readonly INotificationHubNotifier? _notificationHubNotifier;
        private readonly IConfiguration _config;
        private readonly StoryPlatformDbContext _db;
        private readonly IAuthorAiTokenAutoGrantService _authorAiTokenAutoGrant;

        public AdminUserService(
            IUserRepository userRepo,
            IModeratorCategoryAssignmentRepository modCatRepo,
            StoryPlatformDbContext db,
            IConfiguration config,
            IAuthorAiTokenAutoGrantService authorAiTokenAutoGrant,
            INotificationHubNotifier? notificationHubNotifier = null)
        {
            _userRepo = userRepo;
            _modCatRepo = modCatRepo;
            _db = db;
            _config = config;
            _authorAiTokenAutoGrant = authorAiTokenAutoGrant;
            _notificationHubNotifier = notificationHubNotifier;
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
                    phone = request.Phone,
                    id_number = request.IdNumber,
                    settings = "{\"allow_notif\":true}",
                    updated_at = DateTime.UtcNow
                }
            };

            await _userRepo.AddUser(user);

            // Nếu tạo mới với role=AUTHOR thì set hạn mức token mặc định cho tác giả (nếu user chưa có hạn nào).
            if (string.Equals(user.role, "AUTHOR", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _authorAiTokenAutoGrant.OnAuthorBecameAuthorAsync(user.id).ConfigureAwait(false);
                }
                catch { /* best-effort */ }
            }

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
            if (string.Equals(user.status, "BANNED", StringComparison.OrdinalIgnoreCase))
            {
                await _userRepo.DeleteRefreshTokensByUserId(user.id);
                if (_notificationHubNotifier != null)
                {
                    await _notificationHubNotifier.RevokeUserSessionAsync(
                        user.id,
                        "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên để được hỗ trợ.");
                }
                BannedAuthorModerationSweep.Run();
            }
            return true;
        }

        public async Task<bool> SetRoleAsync(Guid id, string role)
        {
            var user = await _userRepo.GetUserById(id);
            if (user == null) return false;

            var prevRole = (user.role ?? "").Trim();
            user.role = (role ?? "").Trim().ToUpperInvariant();
            user.updated_at = DateTime.UtcNow;
            await _userRepo.UpdateUser(user);

            // Không còn gán moderator theo thể loại: xóa mọi bản ghi moderator_category_assignments khi đổi role.
            await _modCatRepo.ReplaceAssignmentsAsync(id, Array.Empty<Guid>());

            // Khi user lần đầu chuyển sang AUTHOR thì set hạn mức token mặc định (không overwrite nếu đã có hạn).
            if (!string.Equals(prevRole, "AUTHOR", StringComparison.OrdinalIgnoreCase)
                && string.Equals(user.role, "AUTHOR", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _authorAiTokenAutoGrant.OnAuthorBecameAuthorAsync(user.id).ConfigureAwait(false);
                }
                catch { /* best-effort */ }
            }

            return true;
        }

        // NOTE: Cơ chế cấp token cho tác giả mới đã chuyển sang singleton rule trong author_ai_token_auto_grant_rules
        // (bảng author_ai_token_auto_grant_rules) và xử lý trong IAuthorAiTokenAutoGrantService.OnAuthorBecameAuthorAsync.

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

            // Vai trò chỉ đổi qua SetRoleAsync; không đồng bộ role từ danh sách thể loại nữa.

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
                Phone = u.user_profiles?.phone,
                IdNumber = u.user_profiles?.id_number,
                IsEmailVerified = u.email_verified_at != null,
                AiTokenLimit = u.ai_token_limit
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
                Phone = u.user_profiles?.phone,
                IdNumber = u.user_profiles?.id_number,
                IsEmailVerified = u.email_verified_at != null,
                AiTokenLimit = u.ai_token_limit
            };
            return dto;
        }
    }
}

