using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class UserActivityLogDAO
    {
        private const string ReadStoryAction = "READ_STORY";
        private const string ReadChapterAction = "READ_CHAPTER";

        /// <summary>Ghi nhận user đã xem story (raw_data = storyId).</summary>
        public static void LogReadStory(Guid userId, Guid storyId, string? ipAddress = null, string? deviceInfo = null)
        {
            using var context = new StoryPlatformDbContext();
            context.user_activity_logs.Add(new user_activity_logs
            {
                user_id = userId,
                action_type = ReadStoryAction,
                description = "User viewed story",
                raw_data = storyId.ToString(),
                ip_address = ipAddress,
                device_info = deviceInfo,
                created_at = DateTime.Now
            });
            context.SaveChanges();
        }

        /// <summary>Ghi nhận user đã xem chapter thuộc story (raw_data = storyId).</summary>
        public static void LogReadChapter(Guid userId, Guid storyId, Guid chapterId, string? ipAddress = null, string? deviceInfo = null)
        {
            using var context = new StoryPlatformDbContext();
            context.user_activity_logs.Add(new user_activity_logs
            {
                user_id = userId,
                action_type = ReadChapterAction,
                description = "User viewed chapter",
                raw_data = storyId.ToString(),
                ip_address = ipAddress,
                device_info = deviceInfo,
                created_at = DateTime.Now
            });
            context.SaveChanges();
        }

        /// <summary>Kiểm tra user đã đọc story chưa (READ_STORY/READ_CHAPTER với raw_data = storyId).</summary>
        public static bool HasReadStory(Guid userId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            var raw = storyId.ToString();
            return context.user_activity_logs.AsNoTracking().Any(l =>
                l.user_id == userId
                && (l.action_type == ReadStoryAction || l.action_type == ReadChapterAction)
                && l.raw_data == raw);
        }

        /// <summary>Kiểm tra user đã đọc ít nhất 1 chapter của story chưa (READ_CHAPTER với raw_data = storyId).</summary>
        public static bool HasReadAnyChapterOfStory(Guid userId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            var raw = storyId.ToString();
            return context.user_activity_logs.AsNoTracking().Any(l =>
                l.user_id == userId
                && l.action_type == ReadChapterAction
                && l.raw_data == raw);
        }
    }
}

