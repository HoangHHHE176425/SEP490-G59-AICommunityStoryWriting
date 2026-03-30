using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    /// <summary>DAO cho bảng follows: theo dõi tác giả (author). user_id theo dõi author_id.</summary>
    public static class FollowDAO
    {
        public sealed class AuthorFollowerListItem
        {
            public Guid UserId { get; set; }
            public string DisplayName { get; set; } = "Người dùng";
            public string Email { get; set; } = "";
            public string? AvatarUrl { get; set; }
            public DateTime? FollowedAt { get; set; }
        }

        /// <summary>Kiểm tra user đã theo dõi tác giả (author) chưa.</summary>
        public static bool IsFollowingAuthor(Guid userId, Guid authorId)
        {
            if (userId == authorId) return false; // không coi là "theo dõi" chính mình
            using var context = new StoryPlatformDbContext();
            return context.follows.Any(f => f.user_id == userId && f.author_id == authorId);
        }

        /// <summary>Theo dõi tác giả. Không cho phép tự theo dõi chính mình. Nếu đã theo dõi thì không thay đổi.</summary>
        public static void FollowAuthor(Guid userId, Guid authorId)
        {
            if (userId == authorId)
                throw new InvalidOperationException("Không thể theo dõi chính mình.");
            using var context = new StoryPlatformDbContext();
            if (context.follows.Any(f => f.user_id == userId && f.author_id == authorId))
                return;
            context.follows.Add(new follows
            {
                user_id = userId,
                author_id = authorId,
                followed_at = DateTime.Now
            });
            context.SaveChanges();
        }

        /// <summary>Bỏ theo dõi tác giả.</summary>
        public static void UnfollowAuthor(Guid userId, Guid authorId)
        {
            using var context = new StoryPlatformDbContext();
            var f = context.follows.FirstOrDefault(x => x.user_id == userId && x.author_id == authorId);
            if (f != null)
            {
                context.follows.Remove(f);
                context.SaveChanges();
            }
        }

        /// <summary>Đếm số lượng người đang theo dõi một author.</summary>
        public static int GetAuthorFollowerCount(Guid authorId)
        {
            using var context = new StoryPlatformDbContext();
            return context.follows
                .AsNoTracking()
                .Count(f => f.author_id == authorId);
        }

        /// <summary>Lấy danh sách user_id đang theo dõi tác giả (để gửi thông báo khi tác giả có chapter/story mới). Loại trừ author_id để không gửi thông báo cho chính tác giả.</summary>
        public static IReadOnlyList<Guid> GetAuthorFollowerIds(Guid authorId)
        {
            using var context = new StoryPlatformDbContext();
            return context.follows
                .AsNoTracking()
                .Where(f => f.author_id == authorId && f.user_id != authorId)
                .Select(f => f.user_id)
                .Distinct()
                .ToList();
        }

        /// <summary>Lấy danh sách author_id mà user đang theo dõi.</summary>
        public static IReadOnlyList<Guid> GetFollowedAuthorIds(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            return context.follows
                .AsNoTracking()
                .Where(f => f.user_id == userId)
                .Select(f => f.author_id)
                .Distinct()
                .ToList();
        }

        /// <summary>Lấy danh sách follower của author theo phân trang.</summary>
        public static (IReadOnlyList<AuthorFollowerListItem> Items, int TotalCount) GetAuthorFollowers(
            Guid authorId,
            int page = 1,
            int pageSize = 20,
            string? search = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            using var context = new StoryPlatformDbContext();

            var query =
                from f in context.follows.AsNoTracking()
                join u in context.users.AsNoTracking() on f.user_id equals u.id
                join up in context.user_profiles.AsNoTracking() on u.id equals up.user_id into profileJoin
                from up in profileJoin.DefaultIfEmpty()
                where f.author_id == authorId && f.user_id != authorId
                select new
                {
                    UserId = u.id,
                    Email = u.email,
                    Nickname = up != null ? up.nickname : null,
                    AvatarUrl = up != null ? up.avatar_url : null,
                    FollowedAt = f.followed_at
                };

            if (!string.IsNullOrWhiteSpace(search))
            {
                var kw = search.Trim().ToLower();
                query = query.Where(x =>
                    (x.Nickname != null && x.Nickname.ToLower().Contains(kw)) ||
                    (x.Email != null && x.Email.ToLower().Contains(kw)));
            }

            var totalCount = query.Count();
            var items = query
                .OrderByDescending(x => x.FollowedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList()
                .Select(x => new AuthorFollowerListItem
                {
                    UserId = x.UserId,
                    Email = x.Email ?? "",
                    DisplayName = string.IsNullOrWhiteSpace(x.Nickname) ? (x.Email ?? "Người dùng") : x.Nickname!,
                    AvatarUrl = string.IsNullOrWhiteSpace(x.AvatarUrl) ? null : x.AvatarUrl,
                    FollowedAt = x.FollowedAt
                })
                .ToList();

            return (items, totalCount);
        }
    }
}
