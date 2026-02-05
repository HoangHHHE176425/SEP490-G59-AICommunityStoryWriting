using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class StoryDAO
    {
        public static IQueryable<stories> GetAll()
        {
            var context = new StoryPlatformDbContext();
            return context.stories.Include(s => s.category).AsNoTracking();
        }

        public static stories? GetById(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.stories.Include(s => s.category).FirstOrDefault(s => s.id == id);
        }

        public static stories? GetBySlug(string slug)
        {
            using var context = new StoryPlatformDbContext();
            return context.stories.FirstOrDefault(s => s.slug == slug);
        }

        public static void Add(stories story)
        {
            using var context = new StoryPlatformDbContext();
            context.stories.Add(story);
            context.SaveChanges();
        }

        public static void AddWithCategories(stories story, IEnumerable<Guid> categoryIds)
        {
            var distinctIds = new HashSet<Guid>(categoryIds ?? Array.Empty<Guid>());
            using var context = new StoryPlatformDbContext();
            context.stories.Add(story);
            context.SaveChanges();
            foreach (var catId in distinctIds)
            {
                context.Database.ExecuteSqlRaw(
                    "INSERT INTO story_categories (story_id, category_id) SELECT {0}, {1} WHERE NOT EXISTS (SELECT 1 FROM story_categories WHERE story_id = {0} AND category_id = {1})",
                    story.id, catId);
            }
        }

        public static void UpdateStoryCategories(Guid storyId, IEnumerable<Guid> categoryIds)
        {
            var distinctIds = new HashSet<Guid>(categoryIds ?? Array.Empty<Guid>());
            using var context = new StoryPlatformDbContext();
            context.Database.ExecuteSqlRaw("DELETE FROM story_categories WHERE story_id = {0}", storyId);
            foreach (var catId in distinctIds)
            {
                context.Database.ExecuteSqlRaw(
                    "INSERT INTO story_categories (story_id, category_id) SELECT {0}, {1} WHERE NOT EXISTS (SELECT 1 FROM story_categories WHERE story_id = {0} AND category_id = {1})",
                    storyId, catId);
            }
        }

        public static void Update(stories story)
        {
            using var context = new StoryPlatformDbContext();
            context.stories.Update(story);
            context.SaveChanges();
        }

        public static void Delete(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            var story = context.stories.FirstOrDefault(s => s.id == id);
            if (story != null)
            {
                context.stories.Remove(story);
                context.SaveChanges();
            }
        }
    }
}
