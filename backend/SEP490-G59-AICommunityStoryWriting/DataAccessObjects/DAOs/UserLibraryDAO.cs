using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public static class UserLibraryDAO
    {
        public const string RelationTypeFollow = "FOLLOW";
        /// <summary>Lịch sử đọc: lưu chapter đang đọc dở (last_read_chapter_id, last_read_at).</summary>
        public const string RelationTypeReading = "READING";

        /// <summary>Kiểm tra user đã theo dõi story chưa (relation_type = FOLLOW).</summary>
        public static bool IsFollowing(Guid userId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            return context.user_library.Any(l => l.user_id == userId && l.story_id == storyId && l.relation_type == RelationTypeFollow);
        }

        /// <summary>Theo dõi story. Nếu đã theo dõi thì không thay đổi. Chỉ cho story PUBLISHED.</summary>
        public static void Follow(Guid userId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            if (context.user_library.Any(l => l.user_id == userId && l.story_id == storyId && l.relation_type == RelationTypeFollow))
                return;
            context.user_library.Add(new user_library
            {
                user_id = userId,
                story_id = storyId,
                relation_type = RelationTypeFollow,
                last_read_chapter_id = null,
                last_read_at = null
            });
            context.SaveChanges();
        }

        /// <summary>Bỏ theo dõi story.</summary>
        public static void Unfollow(Guid userId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            var lib = context.user_library.FirstOrDefault(l => l.user_id == userId && l.story_id == storyId && l.relation_type == RelationTypeFollow);
            if (lib != null)
            {
                context.user_library.Remove(lib);
                context.SaveChanges();
            }
        }

        /// <summary>Lấy danh sách user_id đang theo dõi story (để gửi thông báo khi có chapter mới). relation_type = FOLLOW (không phân biệt hoa thường).</summary>
        public static IReadOnlyList<Guid> GetFollowerUserIds(Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            var list = context.user_library
                .AsNoTracking()
                .Where(l => l.story_id == storyId && l.relation_type != null && l.relation_type.ToUpper() == RelationTypeFollow)
                .Select(l => l.user_id)
                .Distinct()
                .ToList();
            Console.WriteLine($"[CONSOLE] UserLibraryDAO.GetFollowerUserIds StoryId={storyId} count={list.Count} userIds=[{string.Join(", ", list)}]");
            return list;
        }

        /// <summary>Lưu tiến độ đọc: user đang đọc đến chapter nào của story. Tạo hoặc cập nhật bản ghi relation_type = READING.</summary>
        public static void SaveReadingProgress(Guid userId, Guid storyId, Guid chapterId)
        {
            using var context = new StoryPlatformDbContext();
            var lib = context.user_library.FirstOrDefault(l => l.user_id == userId && l.story_id == storyId && l.relation_type == RelationTypeReading);
            var now = DateTime.Now;
            if (lib != null)
            {
                lib.last_read_chapter_id = chapterId;
                lib.last_read_at = now;
                context.SaveChanges();
                return;
            }
            context.user_library.Add(new user_library
            {
                user_id = userId,
                story_id = storyId,
                relation_type = RelationTypeReading,
                last_read_chapter_id = chapterId,
                last_read_at = now
            });
            context.SaveChanges();
        }

        /// <summary>Lấy chapter id và thời điểm đọc cuối cùng của user cho story (relation_type = READING). Trả về (chapterId, lastReadAt) hoặc (null, null) nếu chưa có.</summary>
        public static (Guid? chapterId, DateTime? lastReadAt) GetLastRead(Guid userId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            var lib = context.user_library
                .AsNoTracking()
                .FirstOrDefault(l => l.user_id == userId && l.story_id == storyId && l.relation_type == RelationTypeReading);
            if (lib == null) return (null, null);
            return (lib.last_read_chapter_id, lib.last_read_at);
        }

        /// <summary>Lấy danh sách story_id mà user đang theo dõi (relation_type = FOLLOW).</summary>
        public static IReadOnlyList<Guid> GetFollowedStoryIds(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            return context.user_library
                .AsNoTracking()
                .Where(l => l.user_id == userId && l.relation_type == RelationTypeFollow)
                .Select(l => l.story_id)
                .Distinct()
                .ToList();
        }

        /// <summary>Lấy danh sách lịch sử đọc: (story_id, last_read_chapter_id, last_read_at) cho user (relation_type = READING), sắp xếp last_read_at giảm dần.</summary>
        public static IReadOnlyList<(Guid storyId, Guid chapterId, DateTime lastReadAt)> GetReadingProgressEntries(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            var rows = context.user_library
                .AsNoTracking()
                .Where(l => l.user_id == userId && l.relation_type == RelationTypeReading && l.last_read_chapter_id != null)
                .OrderByDescending(l => l.last_read_at)
                .Select(l => new { l.story_id, l.last_read_chapter_id, l.last_read_at })
                .ToList();
            return rows
                .Where(r => r.last_read_chapter_id.HasValue)
                .Select(r => (r.story_id, r.last_read_chapter_id!.Value, r.last_read_at ?? DateTime.MinValue))
                .ToList();
        }
    }
}
