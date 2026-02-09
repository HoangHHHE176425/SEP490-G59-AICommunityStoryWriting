using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class ChapterDAO
    {
        public static IQueryable<chapters> GetAll()
        {
            var context = new StoryPlatformDbContext();
            return context.chapters.AsNoTracking();
        }

        public static chapters? GetById(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapters.FirstOrDefault(c => c.id == id);
        }

        public static chapters? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapters.FirstOrDefault(c => c.story_id == storyId && c.order_index == orderIndex);
        }

        public static void Add(chapters chapter)
        {
            using var context = new StoryPlatformDbContext();
            context.chapters.Add(chapter);
            context.SaveChanges();
        }

        public static void Update(chapters chapter)
        {
            using var context = new StoryPlatformDbContext();
            context.chapters.Update(chapter);
            context.SaveChanges();
        }

        public static void Delete(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            var chapter = context.chapters.FirstOrDefault(c => c.id == id);
            if (chapter != null)
            {
                context.chapters.Remove(chapter);
                context.SaveChanges();
            }
        }

        /// <summary>Đếm số chương theo story_id (Guid) - dùng cho entity stories.</summary>
        public static int GetCountByStoryId(Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapters.Count(c => c.story_id == storyId);
        }

        /// <summary>Xóa tất cả chapters của một story</summary>
        public static void DeleteByStoryId(Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            var chapters = context.chapters.Where(c => c.story_id == storyId).ToList();
            if (chapters.Any())
            {
                context.chapters.RemoveRange(chapters);
                context.SaveChanges();
            }
        }
    }
}