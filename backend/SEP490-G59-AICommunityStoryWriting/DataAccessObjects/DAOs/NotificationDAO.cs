using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

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
    }
}
