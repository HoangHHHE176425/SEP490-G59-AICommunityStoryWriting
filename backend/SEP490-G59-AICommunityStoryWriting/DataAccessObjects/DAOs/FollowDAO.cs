using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    /// <summary>DAO cho bảng follows: theo dõi tác giả (author). user_id theo dõi author_id.</summary>
    public static class FollowDAO
    {
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
    }
}
