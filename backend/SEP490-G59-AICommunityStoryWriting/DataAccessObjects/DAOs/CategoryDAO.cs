using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class CategoryDAO
    {
        public static IQueryable<categories> GetAll()
        {
            var context = new StoryPlatformDbContext();
            return context.categories.AsNoTracking();
        }

        public static categories? GetById(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.categories.FirstOrDefault(c => c.id == id);
        }

        public static categories? GetBySlug(string slug)
        {
            using var context = new StoryPlatformDbContext();
            return context.categories.FirstOrDefault(c => c.slug == slug);
        }

        public static void Add(categories category)
        {
            using var context = new StoryPlatformDbContext();
            context.categories.Add(category);
            context.SaveChanges();
        }

        public static void Update(categories category)
        {
            using var context = new StoryPlatformDbContext();
            context.categories.Update(category);
            context.SaveChanges();
        }

        public static void Delete(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            var category = context.categories.FirstOrDefault(c => c.id == id);
            if (category != null)
            {
                context.categories.Remove(category);
                context.SaveChanges();
            }
        }

        public static int GetStoryCountByCategoryId(Guid categoryId)
        {
            using var context = new StoryPlatformDbContext();
            return context.stories.Count(s => s.category.Any(c => c.id == categoryId));
        }
    }
}
