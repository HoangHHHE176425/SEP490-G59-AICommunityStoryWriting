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
            try
            {
                // Check if story exists in database
                var existingStory = context.stories.FirstOrDefault(s => s.id == story.id);
                if (existingStory == null)
                {
                    throw new InvalidOperationException($"Story with ID {story.id} not found in database.");
                }

                // Update properties
                existingStory.title = story.title;
                existingStory.slug = story.slug;
                existingStory.summary = story.summary;
                existingStory.status = story.status;
                existingStory.story_progress_status = story.story_progress_status;
                existingStory.cover_image = story.cover_image;
                existingStory.age_rating = story.age_rating;
                existingStory.published_at = story.published_at;
                existingStory.last_published_at = story.last_published_at;
                existingStory.updated_at = story.updated_at;
                existingStory.total_chapters = story.total_chapters;
                existingStory.total_views = story.total_views;
                existingStory.total_favorites = story.total_favorites;
                existingStory.avg_rating = story.avg_rating;
                existingStory.word_count = story.word_count;

                context.SaveChanges();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to update story with ID {story.id}: {ex.Message}", ex);
            }
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