using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccessObjects.DAOs
{
    public static class NotificationDAO
    {
        public static void Add(notifications notification)
        {
            using var context = new StoryPlatformDbContext();
            context.notifications.Add(notification);
            context.SaveChanges();
        }

        public static List<notifications> GetByUserId(Guid userId, int limit = 50, bool onlyUnread = false)
        {
            using var context = new StoryPlatformDbContext();
            IQueryable<notifications> q = context.notifications
                .AsNoTracking()
                .Where(n => n.user_id == userId)
                .OrderByDescending(n => n.created_at);
            if (onlyUnread)
                q = q.Where(n => n.is_read != true);
            return q.Take(limit).ToList();
        }

        public static int GetUnreadCount(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            return context.notifications
                .Count(n => n.user_id == userId && n.is_read != true);
        }

        public static bool MarkAsRead(Guid notificationId, Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            var n = context.notifications.FirstOrDefault(x => x.id == notificationId && x.user_id == userId);
            if (n == null) return false;
            n.is_read = true;
            context.SaveChanges();
            return true;
        }

        public static void MarkAllAsRead(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            foreach (var n in context.notifications.Where(x => x.user_id == userId && x.is_read != true))
                n.is_read = true;
            context.SaveChanges();
        }

        /// <summary>Gửi thông báo cho tất cả user đang follow story khi có chapter mới được publish. Dùng một context và SaveChanges một lần.</summary>
        /// <param name="logger">Nếu có thì ghi log vào host (cùng luồng với EF/core).</param>
        public static void NotifyStoryFollowersNewChapter(Guid storyId, Guid chapterId, string? chapterTitle, string? storyTitle, ILogger? logger = null)
        {
            Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter ENTER StoryId={storyId} ChapterId={chapterId} ChapterTitle={chapterTitle ?? "(null)"} StoryTitle={storyTitle ?? "(null)"}");
            logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter ENTER StoryId={StoryId} ChapterId={ChapterId} ChapterTitle={ChapterTitle} StoryTitle={StoryTitle}",
                storyId, chapterId, chapterTitle ?? "(null)", storyTitle ?? "(null)");

            var followerIds = UserLibraryDAO.GetFollowerUserIds(storyId);
            Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter GetFollowerUserIds StoryId={storyId} FollowerCount={followerIds.Count} UserIds=[{string.Join(", ", followerIds)}]");
            if (followerIds.Count == 0)
            {
                Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter SKIP: no followers for StoryId={storyId} (kiem tra user_library: story_id, relation_type='FOLLOW')");
                logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter SKIP: no followers for StoryId={StoryId} (check user_library.relation_type='FOLLOW')", storyId);
                return;
            }
            logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter StoryId={StoryId} FollowerCount={Count} UserIds={UserIds}",
                storyId, followerIds.Count, string.Join(", ", followerIds));

            var title = "Truyện có chương mới";
            var content = string.IsNullOrWhiteSpace(storyTitle)
                ? "Truyện bạn theo dõi vừa ra chương mới."
                : $"«{storyTitle.Trim()}» vừa ra chương mới" + (string.IsNullOrWhiteSpace(chapterTitle) ? "." : $": {chapterTitle.Trim()}");
            var linkUrl = $"/Chapters/Read/{chapterId}";
            using var context = new StoryPlatformDbContext();
            var now = DateTime.Now;
            foreach (var userId in followerIds)
            {
                context.notifications.Add(new notifications
                {
                    id = Guid.NewGuid(),
                    user_id = userId,
                    type = "STORY_CHAPTER_PUBLISHED",
                    title = title,
                    content = content,
                    link_url = linkUrl,
                    is_read = false,
                    created_at = now
                });
            }
            try
            {
                context.SaveChanges();
                Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter OK: saved {followerIds.Count} notifications for StoryId={storyId}");
                logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter OK: saved {Count} notifications for StoryId={StoryId}", followerIds.Count, storyId);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter ERROR StoryId={storyId} ex={ex.Message}");
                logger?.LogError(ex, "NotifyStoryFollowersNewChapter ERROR StoryId={StoryId}", storyId);
                throw;
            }
        }
    }
}
