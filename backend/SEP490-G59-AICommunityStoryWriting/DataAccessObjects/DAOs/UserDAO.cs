using BusinessObjects;
using BusinessObjects.Account;
using BusinessObjects.Entities;
using DataAccessObjects.Queries;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DataAccessObjects.DAOs
{
    public class UserDAO
    {
        private static UserDAO instance = null;
        private static readonly object instanceLock = new object();
        public static UserDAO Instance
        {
            get { lock (instanceLock) { return instance ??= new UserDAO(); } }
        }

        public async Task<users?> FindUserByEmail(StoryPlatformDbContext context, string email)
        {
            return await context.users.Include(u => u.user_profiles)
                                      .FirstOrDefaultAsync(u => u.email == email);
        }

        public async Task<users?> FindUserById(StoryPlatformDbContext context, Guid id)
        {
            // Do not Include(stories): tracking many stories on every GetUserById caused SaveChanges to UPDATE
            // story rows (or hit concurrency) during profile/password/admin updates. Use GetAuthorStoryAggregatesAsync for stats.
            return await context.users
                .Include(u => u.user_profiles)
                .FirstOrDefaultAsync(u => u.id == id);
        }

        /// <summary>Aggregates for profile/stats without loading every story entity into the change tracker.</summary>
        public async Task<(int StoryCount, long TotalViews, int TotalFavorites)> GetAuthorStoryAggregatesAsync(
            StoryPlatformDbContext context,
            Guid authorId)
        {
            var stats = await context.stories
                .AsNoTracking()
                .Where(s => s.author_id == authorId)
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    Count = g.Count(),
                    TotalViews = g.Sum(s => (long)(s.total_views ?? 0)),
                    TotalFavorites = g.Sum(s => s.total_favorites ?? 0)
                })
                .FirstOrDefaultAsync();

            return stats == null
                ? (0, 0L, 0)
                : (stats.Count, stats.TotalViews, stats.TotalFavorites);
        }

        public Task<bool> UserExistsAsync(StoryPlatformDbContext context, Guid userId)
            => context.users.AnyAsync(u => u.id == userId);

        public async Task<string?> GetUserProfileNicknameAsync(StoryPlatformDbContext context, Guid userId)
        {
            return await context.user_profiles
                .AsNoTracking()
                .Where(p => p.user_id == userId)
                .Select(p => p.nickname)
                .FirstOrDefaultAsync();
        }

        /// <summary>
        /// Loads only <see cref="users"/> and <see cref="user_profiles"/> for this user, then saves.
        /// Avoids re-attaching unrelated tracked entities that can cause UPDATE ... 0 rows (concurrency exceptions).
        /// </summary>
        public async Task PersistUserProfileAsync(StoryPlatformDbContext context, Guid userId, UserProfilePersistModel model)
        {
            var user = await context.users.FirstOrDefaultAsync(u => u.id == userId);
            if (user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var profile = await context.user_profiles.FirstOrDefaultAsync(p => p.user_id == userId);
            if (profile == null)
            {
                profile = new user_profiles
                {
                    user_id = userId,
                    social_links = "{}",
                    settings = "{\"allow_notif\": true}"
                };
                context.user_profiles.Add(profile);
            }

            if (model.SetNickname)
            {
                profile.nickname = model.Nickname;
            }

            if (model.SetPhone)
            {
                profile.phone = model.Phone;
            }

            if (model.SetIdNumber)
            {
                profile.id_number = model.IdNumber;
            }

            if (model.SetBio)
            {
                profile.bio = model.Bio;
            }

            if (model.SetDescription)
            {
                profile.description = model.Description;
            }

            if (model.SetAvatarUrl)
            {
                profile.avatar_url = model.AvatarUrl;
            }

            var now = DateTime.UtcNow;
            profile.updated_at = now;
            user.updated_at = now;

            await context.SaveChangesAsync();
        }

        public async Task<bool> CheckEmailExists(StoryPlatformDbContext context, string email)
        {
            return await context.users.AnyAsync(u => u.email == email);
        }

        public async Task AddUser(StoryPlatformDbContext context, users user)
        {
            context.users.Add(user);
            await context.SaveChangesAsync();
        }

        public async Task UpdateUser(StoryPlatformDbContext context, users user)
        {
            // Do not use DbSet.Update(user): it marks the whole tracked graph Modified and can UPDATE unrelated rows.
            var entry = context.Entry(user);
            if (entry.State == EntityState.Detached)
            {
                context.Attach(user);
                entry.State = EntityState.Modified;
            }

            await context.SaveChangesAsync();
        }

        public async Task AddToken(StoryPlatformDbContext context, auth_tokens token)
        {
            context.auth_tokens.Add(token);
            await context.SaveChangesAsync();
        }

        public async Task<auth_tokens?> GetRefreshToken(StoryPlatformDbContext context, string refreshToken)
        {
            var now = DateTime.UtcNow;
            return await context.auth_tokens
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.refresh_token == refreshToken && t.expires_at > now);
        }

        public async Task DeleteRefreshToken(StoryPlatformDbContext context, string refreshToken)
        {
            var token = await context.auth_tokens.FirstOrDefaultAsync(t => t.refresh_token == refreshToken);
            if (token != null)
            {
                context.auth_tokens.Remove(token);
                await context.SaveChangesAsync();
            }
        }

        public async Task DeleteRefreshTokensByUserId(StoryPlatformDbContext context, Guid userId)
        {
            var tokens = await context.auth_tokens
                .Where(t => t.user_id == userId)
                .ToListAsync();

            if (tokens.Count == 0)
            {
                return;
            }

            context.auth_tokens.RemoveRange(tokens);
            await context.SaveChangesAsync();
        }

        /// <summary>
        /// Bật cờ must_resign_policy cho các AUTHOR chưa ký policy AUTHOR đang active.
        /// </summary>
        public async Task<int> MarkAuthorsMustResignPolicyAsync(StoryPlatformDbContext context, Guid activeAuthorPolicyId)
        {
            var policyEffectiveFrom = await context.system_policies
                .AsNoTracking()
                .Where(p => p.id == activeAuthorPolicyId)
                .Select(p => p.activated_at ?? p.created_at)
                .FirstOrDefaultAsync() ?? DateTime.MinValue;

            var acceptedAuthorIds = await context.author_policy_acceptances
                .AsNoTracking()
                .Where(a => a.policy_id == activeAuthorPolicyId && a.accepted_at >= policyEffectiveFrom)
                .Select(a => a.user_id)
                .Distinct()
                .ToListAsync();

            var targetAuthors = await context.users
                .Where(u =>
                    (u.role ?? "").ToUpper() == "AUTHOR" &&
                    (u.status ?? "").ToUpper() == "ACTIVE" &&
                    !acceptedAuthorIds.Contains(u.id))
                .ToListAsync();

            if (targetAuthors.Count == 0) return 0;

            var now = DateTime.UtcNow;
            foreach (var author in targetAuthors)
            {
                author.must_resign_policy = true;
                author.updated_at = now;
            }

            await context.SaveChangesAsync();
            return targetAuthors.Count;
        }

        /// <summary>
        /// Xóa cờ must_resign_policy cho tất cả AUTHOR (dùng khi policy active không bắt buộc ký lại).
        /// </summary>
        public async Task<int> ClearAuthorMustResignPolicyFlagAsync(StoryPlatformDbContext context)
        {
            var authors = await context.users
                .Where(u =>
                    (u.role ?? "").ToUpper() == "AUTHOR" &&
                    u.must_resign_policy == true)
                .ToListAsync();

            if (authors.Count == 0) return 0;

            var now = DateTime.UtcNow;
            foreach (var author in authors)
            {
                author.must_resign_policy = false;
                author.updated_at = now;
            }

            await context.SaveChangesAsync();
            return authors.Count;
        }

        public async Task<bool> IsNicknameExist(StoryPlatformDbContext context, string nickname, Guid currentUserId)
        {
            return await context.user_profiles
                .AnyAsync(p => p.nickname == nickname && p.user_id != currentUserId);
        }
        public async Task SoftDeleteUser(StoryPlatformDbContext context, Guid userId)
        {
            var user = await context.users.FindAsync(userId);
            if (user != null)
            {

                user.status = "DELETED";


                user.email = $"deleted_{Guid.NewGuid()}@deleted.store";


                user.password_hash = "DELETED_USER_" + Guid.NewGuid().ToString();

                user.updated_at = DateTime.UtcNow;

                await context.SaveChangesAsync();
            }
        }
        public static bool Exists(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.users.Any(u => u.id == id);
        }

        /// <summary>Tìm user id theo email hoặc nickname (chứa chuỗi) — dùng lọc moderator performance.</summary>
        public static List<Guid> SearchUserIdsByEmailOrNickname(string search)
        {
            if (string.IsNullOrWhiteSpace(search))
                return new List<Guid>();
            using var context = new StoryPlatformDbContext();
            var s = search.Trim().ToLowerInvariant();
            return context.users
                .AsNoTracking()
                .Include(u => u.user_profiles)
                .Where(u => u.email.ToLower().Contains(s) ||
                    (u.user_profiles != null && u.user_profiles.nickname != null && u.user_profiles.nickname.ToLower().Contains(s)))
                .Select(u => u.id)
                .ToList();
        }

        /// <summary>Moderator đang ACTIVE — dùng khi admin giao lại lock duyệt (chỉ role MODERATOR).</summary>
        public static bool IsActiveModerator(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            var u = context.users.AsNoTracking().FirstOrDefault(x => x.id == id);
            if (u == null)
                return false;
            if (string.Equals(u.status, "ACTIVE", StringComparison.OrdinalIgnoreCase) != true)
                return false;
            return string.Equals(u.role, "MODERATOR", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>COMPLIANCE đang ACTIVE — dùng khi admin giao lại lock báo cáo truyện.</summary>
        public static bool IsActiveComplianceOfficer(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            var u = context.users.AsNoTracking().FirstOrDefault(x => x.id == id);
            if (u == null)
                return false;
            if (string.Equals(u.status, "ACTIVE", StringComparison.OrdinalIgnoreCase) != true)
                return false;
            return string.Equals(u.role, "COMPLIANCE", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Danh sách COMPLIANCE ACTIVE + số lock báo cáo truyện đang giữ (COMPLIANCE_STORY_REPORTS).</summary>
        public static List<(Guid Id, string DisplayName, string? Email, int ComplianceStoryReportLockCount)> ListActiveComplianceOfficersForStoryReportAssignment()
        {
            var counts = ReviewAssignmentDAO.GetClaimedAssignmentCountsByAssigneeForTargetType(
                ReviewAssignmentDAO.TargetTypeComplianceStoryReports);
            using var context = new StoryPlatformDbContext();
            var list = context.users
                .AsNoTracking()
                .Include(u => u.user_profiles)
                .Where(u => (u.status ?? "").ToUpper() == "ACTIVE" &&
                    (u.role ?? "").ToUpper() == "COMPLIANCE")
                .OrderBy(u => u.email)
                .ToList();
            var rows = list.Select(u =>
            {
                var name = u.user_profiles?.nickname?.Trim();
                if (string.IsNullOrEmpty(name))
                    name = u.email;
                var n = counts.TryGetValue(u.id, out var c) ? c : 0;
                return (Id: u.id, DisplayName: name ?? "–", Email: (string?)u.email, ComplianceStoryReportLockCount: n);
            }).ToList();
            return rows
                .OrderBy(x => x.ComplianceStoryReportLockCount)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>Danh sách moderator ACTIVE — giao lại lock story/chapter (không gồm admin).</summary>
        public static List<(Guid Id, string DisplayName, int ClaimedAssignmentCount)> ListActiveModeratorsForAssignment()
        {
            var counts = ReviewAssignmentDAO.GetClaimedAssignmentCountsByAssignee();
            using var context = new StoryPlatformDbContext();
            var list = context.users
                .AsNoTracking()
                .Include(u => u.user_profiles)
                .Where(u => (u.status ?? "").ToUpper() == "ACTIVE" &&
                    (u.role ?? "").ToUpper() == "MODERATOR")
                .OrderBy(u => u.email)
                .ToList();
            var rows = list.Select(u =>
            {
                var name = u.user_profiles?.nickname?.Trim();
                if (string.IsNullOrEmpty(name))
                    name = u.email;
                var n = counts.TryGetValue(u.id, out var c) ? c : 0;
                return (Id: u.id, DisplayName: name ?? "–", ClaimedAssignmentCount: n);
            }).ToList();
            return rows
                .OrderBy(x => x.ClaimedAssignmentCount)
                .ThenBy(x => x.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<(IEnumerable<users> Items, int TotalCount)> GetUsersAsync(
            StoryPlatformDbContext context,
            AdminUserQuery query)
        {
            var q = context.users
                .Include(u => u.user_profiles)
                .AsQueryable();

            var isIdSearch = false;
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var rawSearch = query.Search.Trim();
                if (Guid.TryParse(rawSearch, out var userId))
                {
                    // If the input is a UUID, prioritize an exact id lookup.
                    isIdSearch = true;
                    q = q.Where(u => u.id == userId);
                }
                else
                {
                    var s = rawSearch.ToLowerInvariant();
                    q = q.Where(u =>
                        u.email.ToLower().Contains(s) ||
                        (u.user_profiles != null && u.user_profiles.nickname != null && u.user_profiles.nickname.ToLower().Contains(s)) ||
                        (u.user_profiles != null && u.user_profiles.phone != null && u.user_profiles.phone.ToLower().Contains(s)) ||
                        (u.user_profiles != null && u.user_profiles.id_number != null && u.user_profiles.id_number.ToLower().Contains(s)));
                }
            }

            if (!isIdSearch && !string.IsNullOrWhiteSpace(query.Role))
            {
                var r = query.Role.Trim().ToUpperInvariant();
                q = q.Where(u => (u.role ?? "").ToUpper() == r);
            }

            if (!isIdSearch && !string.IsNullOrWhiteSpace(query.Status))
            {
                var st = query.Status.Trim().ToUpperInvariant();
                q = q.Where(u => (u.status ?? "").ToUpper() == st);
            }

            var total = await q.CountAsync();

            var sortBy = (query.SortBy ?? "").Trim().ToLowerInvariant();
            var asc = (query.SortOrder ?? "desc").Trim().ToLowerInvariant() == "asc";

            q = sortBy switch
            {
                "email" => asc ? q.OrderBy(u => u.email) : q.OrderByDescending(u => u.email),
                "role" => asc ? q.OrderBy(u => u.role) : q.OrderByDescending(u => u.role),
                "status" => asc ? q.OrderBy(u => u.status) : q.OrderByDescending(u => u.status),
                _ => asc ? q.OrderBy(u => u.created_at) : q.OrderByDescending(u => u.created_at)
            };

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

            var items = await q.AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<(int Total, int Active, int Inactive, int Banned, int Pending, int Authors, int Moderators)> GetStatsAsync(StoryPlatformDbContext context)
        {
            // Normalize to upper-case comparisons.
            IQueryable<users> q = context.users.AsNoTracking();

            var total = await q.CountAsync();
            var active = await q.CountAsync(u => (u.status ?? "").ToUpper() == "ACTIVE");
            var inactive = await q.CountAsync(u => (u.status ?? "").ToUpper() == "INACTIVE");
            var banned = await q.CountAsync(u => (u.status ?? "").ToUpper() == "BANNED");
            var pending = await q.CountAsync(u => (u.status ?? "").ToUpper() == "PENDING");

            var authors = await q.CountAsync(u => (u.role ?? "").ToUpper() == "AUTHOR");
            var moderators = await q.CountAsync(u => (u.role ?? "").ToUpper() == "MODERATOR");

            return (total, active, inactive, banned, pending, authors, moderators);
        }

        public static bool IsAuthorWritingSuspended(Guid authorUserId)
        {
            if (authorUserId == Guid.Empty) return false;
            using var context = new StoryPlatformDbContext();
            var until = context.users.AsNoTracking()
                .Where(u => u.id == authorUserId)
                .Select(u => u.author_writing_suspended_until)
                .FirstOrDefault();
            return until.HasValue && until.Value > DateTime.UtcNow;
        }

        /// <summary>Số user có role AUTHOR, không tính tài khoản đã ban (thống kê công khai /community/stats).</summary>
        public static int CountAuthorsExcludingBanned()
        {
            using var context = new StoryPlatformDbContext();
            return context.users.AsNoTracking().Count(u =>
                (u.role ?? "").ToUpper() == "AUTHOR" &&
                (u.status ?? "").ToUpper() != "BANNED");
        }

        /// <summary>Kiểm tra users.status == BANNED (thư viện / lọc danh sách công khai).</summary>
        public static bool IsAccountBanned(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            var u = context.users.AsNoTracking().FirstOrDefault(x => x.id == userId);
            return u != null && string.Equals(u.status, "BANNED", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>Giá trị users.status (ACTIVE, BANNED, …) cho DTO hiển thị nội bộ.</summary>
        public static string? GetAccountStatus(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            var u = context.users.AsNoTracking().FirstOrDefault(x => x.id == userId);
            return u?.status;
        }

        /// <summary>Mốc đình chỉ quyền viết (UTC) — dùng cho thông báo lỗi / DTO.</summary>
        public static DateTime? GetAuthorWritingSuspendedUntilUtc(Guid userId)
        {
            if (userId == Guid.Empty) return null;
            using var context = new StoryPlatformDbContext();
            return context.users.AsNoTracking()
                .Where(u => u.id == userId)
                .Select(u => u.author_writing_suspended_until)
                .FirstOrDefault();
        }

        /// <summary>Batch: status + author_writing_suspended_until cho hàng đợi compliance.</summary>
        public static Dictionary<Guid, (string? Status, DateTime? AuthorWritingSuspendedUntil)> GetUsersModerationSnapshot(
            IReadOnlyCollection<Guid> userIds)
        {
            var result = new Dictionary<Guid, (string?, DateTime?)>();
            if (userIds == null || userIds.Count == 0) return result;
            var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (ids.Count == 0) return result;
            using var context = new StoryPlatformDbContext();
            var rows = context.users.AsNoTracking()
                .Where(u => ids.Contains(u.id))
                .Select(u => new { u.id, u.status, u.author_writing_suspended_until })
                .ToList();
            foreach (var r in rows)
                result[r.id] = (r.status, r.author_writing_suspended_until);
            return result;
        }

        /// <summary>Batch: display name ưu tiên nickname, fallback email.</summary>
        public static Dictionary<Guid, string> GetDisplayNamesByIds(IReadOnlyCollection<Guid> userIds)
        {
            var result = new Dictionary<Guid, string>();
            if (userIds == null || userIds.Count == 0) return result;
            var ids = userIds.Where(id => id != Guid.Empty).Distinct().ToList();
            if (ids.Count == 0) return result;

            using var context = new StoryPlatformDbContext();
            var rows = context.users.AsNoTracking()
                .Include(u => u.user_profiles)
                .Where(u => ids.Contains(u.id))
                .Select(u => new
                {
                    u.id,
                    Nick = u.user_profiles != null ? u.user_profiles.nickname : null,
                    u.email
                })
                .ToList();

            foreach (var r in rows)
            {
                var name = string.IsNullOrWhiteSpace(r.Nick) ? r.email : r.Nick;
                if (!string.IsNullOrWhiteSpace(name))
                    result[r.id] = name.Trim();
            }
            return result;
        }

        public static void SetAuthorWritingSuspendedUntil(Guid userId, DateTime? untilUtc)
        {
            if (userId == Guid.Empty) return;
            using var context = new StoryPlatformDbContext();
            var u = context.users.FirstOrDefault(x => x.id == userId);
            if (u == null) return;
            u.author_writing_suspended_until = untilUtc;
            u.updated_at = DateTime.UtcNow;
            context.SaveChanges();
        }

        public static long GetAiTokenLimit(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            return context.users.AsNoTracking()
                .Where(u => u.id == userId)
                .Select(u => u.ai_token_limit)
                .FirstOrDefault();
        }

        /// <summary>Trừ token AI khỏi số dư. Không cho phép âm (nếu thiếu thì clamp về 0).</summary>
        public static void DebitAiTokenLimit(Guid userId, long tokens)
        {
            if (userId == Guid.Empty || tokens <= 0) return;
            using var context = new StoryPlatformDbContext();
            var u = context.users.FirstOrDefault(x => x.id == userId);
            if (u == null) return;
            var cur = u.ai_token_limit;
            var next = cur - tokens;
            u.ai_token_limit = next >= 0 ? next : 0;
            u.updated_at = DateTime.UtcNow;
            context.SaveChanges();
        }

        /// <summary>Cộng token AI vào số dư.</summary>
        public static void CreditAiTokenLimit(Guid userId, long tokens)
        {
            if (userId == Guid.Empty || tokens <= 0) return;
            using var context = new StoryPlatformDbContext();
            var u = context.users.FirstOrDefault(x => x.id == userId);
            if (u == null) return;
            u.ai_token_limit = checked(u.ai_token_limit + tokens);
            u.updated_at = DateTime.UtcNow;
            context.SaveChanges();
        }

        public static void SetUserAccountStatus(Guid userId, string status)
        {
            using var context = new StoryPlatformDbContext();
            var u = context.users.FirstOrDefault(x => x.id == userId)
                ?? throw new InvalidOperationException("User not found.");
            u.status = (status ?? "").Trim().ToUpperInvariant();
            u.updated_at = DateTime.UtcNow;
            context.SaveChanges();
        }

        /// <summary>Email đăng ký — dùng thông báo (ví dụ sau khi admin duyệt cấm tài khoản).</summary>
        public static string? GetUserEmail(Guid userId)
        {
            if (userId == Guid.Empty) return null;
            using var context = new StoryPlatformDbContext();
            return context.users.AsNoTracking()
                .Where(u => u.id == userId)
                .Select(u => u.email)
                .FirstOrDefault();
        }

        /// <summary>Đặt một hoặc nhiều giới hạn token AI; chỉ cập nhật trường khi cờ set tương ứng là true (null = bỏ giới hạn cột đó).</summary>
        public async Task<int> SetAuthorAiTokenBudgetLimitsAsync(
            StoryPlatformDbContext context,
            Guid userId,
            bool setLifetime,
            long? lifetimeLimit,
            bool setPerDay,
            long? perDayLimit,
            bool setPerWeek,
            long? perWeekLimit,
            bool setPerMonth,
            long? perMonthLimit,
            CancellationToken cancellationToken = default)
        {
            var u = await context.users.FirstOrDefaultAsync(x => x.id == userId, cancellationToken);
            if (u == null) return 0;
            // Schema mới: chỉ dùng users.ai_token_limit (số dư token).
            // Giữ signature để tương thích ngược; chỉ lấy lifetimeLimit.
            _ = setPerDay;
            _ = perDayLimit;
            _ = setPerWeek;
            _ = perWeekLimit;
            _ = setPerMonth;
            _ = perMonthLimit;
            if (setLifetime)
                u.ai_token_limit = Math.Max(0L, lifetimeLimit ?? 0L);
            u.updated_at = DateTime.UtcNow;
            return await context.SaveChangesAsync(cancellationToken);
        }
    }
}
