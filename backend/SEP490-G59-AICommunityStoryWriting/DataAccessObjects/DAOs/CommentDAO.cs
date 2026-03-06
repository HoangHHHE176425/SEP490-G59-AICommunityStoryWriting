using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class CommentDAO
    {
        /// <summary>Đếm số comment theo story_id (Guid). Mặc định chỉ đếm comment status = APPROVED.</summary>
        public static int GetCountByStoryId(Guid storyId, string status = "APPROVED")
        {
            using var context = new StoryPlatformDbContext();
            return context.comments.AsNoTracking()
                .Count(c => c.story_id == storyId && c.status == status);
        }

        public static comments? GetById(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.comments.AsNoTracking().FirstOrDefault(c => c.id == id);
        }

        public static IReadOnlyList<comments> GetStoryComments(Guid storyId, string status = "APPROVED")
        {
            using var context = new StoryPlatformDbContext();
            return context.comments.AsNoTracking()
                .Include(c => c.userNavigation)
                .ThenInclude(u => u.user_profiles)
                .Where(c => c.story_id == storyId && c.status == status)
                .OrderBy(c => c.created_at)
                .ToList();
        }

        public static comments AddStoryComment(Guid storyId, Guid userId, string content, Guid? parentId = null, string status = "APPROVED")
        {
            using var context = new StoryPlatformDbContext();

            var entity = new comments
            {
                id = Guid.NewGuid(),
                user_id = userId,
                story_id = storyId,
                chapter_id = null,
                parent_id = parentId,
                content = content,
                likes_count = 0,
                status = status,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            context.comments.Add(entity);
            context.SaveChanges();

            // Re-load with navigation for mapping in upper layer
            return context.comments.AsNoTracking()
                .Include(c => c.userNavigation)
                .ThenInclude(u => u.user_profiles)
                .First(c => c.id == entity.id);
        }

        /// <summary>Kiểm tra user đã like comment chưa (bảng comment_likes). Trả về false nếu bảng chưa có hoặc lỗi.</summary>
        public static bool HasLiked(Guid userId, Guid commentId)
        {
            try
            {
                using var context = new StoryPlatformDbContext();
                var count = context.Database
                    .SqlQuery<int>($"SELECT CAST(COUNT(1) AS int) FROM comment_likes WHERE user_id = {userId} AND comment_id = {commentId}")
                    .FirstOrDefault();
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>Bật/tắt like: 1 user chỉ 1 like/comment. Trả về true = đã like, false = đã bỏ like. Dùng quan hệ many-to-many thay vì raw SQL.</summary>
        public static bool ToggleLike(Guid userId, Guid commentId)
        {
            using var context = new StoryPlatformDbContext();
            var user = context.users.Include(u => u.comment).FirstOrDefault(u => u.id == userId);
            var comment = context.comments.FirstOrDefault(c => c.id == commentId);
            if (user == null || comment == null) return false;

            var alreadyLiked = user.comment.Any(c => c.id == commentId);
            if (alreadyLiked)
            {
                user.comment.Remove(comment);
                comment.likes_count = Math.Max(0, (comment.likes_count ?? 1) - 1);
            }
            else
            {
                user.comment.Add(comment);
                comment.likes_count = (comment.likes_count ?? 0) + 1;
            }
            context.SaveChanges();
            return !alreadyLiked;
        }
    }
}

