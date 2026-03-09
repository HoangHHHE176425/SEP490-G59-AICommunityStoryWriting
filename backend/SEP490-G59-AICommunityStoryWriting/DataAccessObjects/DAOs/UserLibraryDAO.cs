using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public static class UserLibraryDAO
    {
        public const string RelationTypeFollow = "FOLLOW";

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
    }
}
