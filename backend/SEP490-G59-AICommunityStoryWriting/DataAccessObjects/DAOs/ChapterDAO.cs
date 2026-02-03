using BusinessObjects.DataAccessObjects.Context;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class ChapterDAO
    {
        public static IQueryable<chapter> GetAll()
        {
            var context = new StoryPlatformDbContext();
            return context.chapters.AsNoTracking();
        }

        public static chapter? GetById(int id)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapters.FirstOrDefault(c => c.id == id);
        }

        public static chapter? GetByStoryIdAndOrderIndex(int storyId, int orderIndex)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapters.FirstOrDefault(c => c.story_id == storyId && c.order_index == orderIndex);
        }

        public static void Add(chapter chapter)
        {
            using var context = new StoryPlatformDbContext();
            context.chapters.Add(chapter);
            context.SaveChanges();
        }

        public static void Update(chapter chapter)
        {
            using var context = new StoryPlatformDbContext();
            context.chapters.Update(chapter);
            context.SaveChanges();
        }

        public static void Delete(int id)
        {
            using var context = new StoryPlatformDbContext();
            var chapter = context.chapters.FirstOrDefault(c => c.id == id);
            if (chapter != null)
            {
                context.chapters.Remove(chapter);
                context.SaveChanges();
            }
        }
    }
}
