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

        /// <summary>L?y danh s?ch story id c? ?t nh?t m?t category n?m trong categoryIds (d?ng cho moderator).</summary>
        public static List<Guid> GetIdsByCategoryIds(IReadOnlyCollection<Guid> categoryIds)
        {
            if (categoryIds == null || categoryIds.Count == 0)
                return new List<Guid>();
            using var context = new StoryPlatformDbContext();
            var ids = new List<Guid>();
            foreach (var catId in categoryIds)
            {
                var storyIds = context.Database.SqlQueryRaw<Guid>(
                    "SELECT story_id FROM story_categories WHERE category_id = {0}", catId).ToList();
                foreach (var sid in storyIds)
                    if (!ids.Contains(sid)) ids.Add(sid);
            }
            return ids;
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

        /// <summary>T?ng total_views c?a story l?n 1 (d?ng khi ghi nh?n l??t xem h?p l?, ch?ng spam ? t?ng service).</summary>
        public static void IncrementViewCount(Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            var story = context.stories.FirstOrDefault(s => s.id == storyId);
            if (story != null)
            {
                story.total_views = (story.total_views ?? 0) + 1;
                context.SaveChanges();
            }
        }

        /// <summary>C?p nh?t avg_rating cho story.</summary>
        public static void UpdateAvgRating(Guid storyId, decimal avgRating)
        {
            using var context = new StoryPlatformDbContext();
            var story = context.stories.FirstOrDefault(s => s.id == storyId);
            if (story != null)
            {
                story.avg_rating = avgRating;
                context.SaveChanges();
            }
        }

        public static void SetCommentsDisabled(Guid storyId, bool disabled)
        {
            using var context = new StoryPlatformDbContext();
            var story = context.stories.FirstOrDefault(s => s.id == storyId)
                ?? throw new InvalidOperationException("Story not found.");
            story.comments_disabled = disabled;
            story.updated_at = DateTime.UtcNow;
            context.SaveChanges();
        }

        public static void SetComplianceHidden(Guid storyId, bool hidden)
        {
            using var context = new StoryPlatformDbContext();
            var story = context.stories.FirstOrDefault(s => s.id == storyId)
                ?? throw new InvalidOperationException("Story not found.");
            story.compliance_hidden = hidden;
            story.updated_at = DateTime.UtcNow;
            context.SaveChanges();
        }

        public static void SetComplianceFlag(Guid storyId, bool flagged, string? note, Guid? flaggedByUserId)
        {
            using var context = new StoryPlatformDbContext();
            var story = context.stories.FirstOrDefault(s => s.id == storyId)
                ?? throw new InvalidOperationException("Story not found.");
            story.compliance_flagged = flagged;
            story.compliance_flag_note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
            if (flagged)
            {
                story.compliance_flagged_at = DateTime.UtcNow;
                story.compliance_flagged_by = flaggedByUserId;
            }
            else
            {
                story.compliance_flagged_at = null;
                story.compliance_flagged_by = null;
                story.compliance_flag_note = null;
            }
            story.updated_at = DateTime.UtcNow;
            context.SaveChanges();
        }
    }
}