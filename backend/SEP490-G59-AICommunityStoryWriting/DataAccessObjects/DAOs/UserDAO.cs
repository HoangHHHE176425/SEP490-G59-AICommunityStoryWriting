using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.Queries;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

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
            return await context.users
                .Include(u => u.user_profiles)
                .Include(u => u.stories) // Include truyện để đếm view, like, số lượng
                .FirstOrDefaultAsync(u => u.id == id);
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
            context.users.Update(user);
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

                context.users.Update(user);
                await context.SaveChangesAsync();
            }
        }
        public static bool Exists(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.users.Any(u => u.id == id);
        }

        public async Task<(IEnumerable<users> Items, int TotalCount)> GetUsersAsync(
            StoryPlatformDbContext context,
            AdminUserQuery query)
        {
            var q = context.users
                .Include(u => u.user_profiles)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim().ToLowerInvariant();
                q = q.Where(u =>
                    u.email.ToLower().Contains(s) ||
                    (u.user_profiles != null && u.user_profiles.nickname != null && u.user_profiles.nickname.ToLower().Contains(s)) ||
                    (u.user_profiles != null && u.user_profiles.Phone != null && u.user_profiles.Phone.ToLower().Contains(s)) ||
                    (u.user_profiles != null && u.user_profiles.IdNumber != null && u.user_profiles.IdNumber.ToLower().Contains(s)));
            }

            if (!string.IsNullOrWhiteSpace(query.Role))
            {
                var r = query.Role.Trim().ToUpperInvariant();
                q = q.Where(u => (u.role ?? "").ToUpper() == r);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
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
    }
}
