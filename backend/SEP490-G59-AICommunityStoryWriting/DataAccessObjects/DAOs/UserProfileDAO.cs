using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public static class UserProfileDAO
    {
        /// <summary>
        /// Lấy avatar_url theo nhiều user_id (một query) — dùng cho danh sách truyện, tránh N+1.
        /// </summary>
        public static Dictionary<Guid, string?> GetAvatarUrlsByUserIds(IEnumerable<Guid> userIds)
        {
            var ids = userIds.Distinct().ToList();
            if (ids.Count == 0)
                return new Dictionary<Guid, string?>();

            using var context = new StoryPlatformDbContext();
            var rows = context.user_profiles
                .AsNoTracking()
                .Where(p => ids.Contains(p.user_id))
                .Select(p => new { p.user_id, p.avatar_url })
                .ToList();

            var dict = new Dictionary<Guid, string?>();
            foreach (var r in rows)
            {
                var url = string.IsNullOrWhiteSpace(r.avatar_url) ? null : r.avatar_url.Trim();
                dict[r.user_id] = url;
            }

            return dict;
        }

        /// <summary>Avatar công khai cho một user (chi tiết truyện / đơn lẻ).</summary>
        public static string? GetAvatarUrlForUser(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            var url = context.user_profiles
                .AsNoTracking()
                .Where(p => p.user_id == userId)
                .Select(p => p.avatar_url)
                .FirstOrDefault();
            return string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        }
    }
}
