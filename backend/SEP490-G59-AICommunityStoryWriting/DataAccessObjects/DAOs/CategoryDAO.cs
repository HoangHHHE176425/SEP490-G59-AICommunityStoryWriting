using BusinessObjects.DataAccessObjects.Context;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class CategoryDAO
    {
        public static IQueryable<category> GetAll()
        {
            var context = new StoryPlatformDbContext();
            return context.categories.AsNoTracking();
        }

        public static category? GetById(int id)
        {
            using var context = new StoryPlatformDbContext();
            return context.categories.FirstOrDefault(c => c.id == id);
        }

        public static category? GetBySlug(string slug)
        {
            using var context = new StoryPlatformDbContext();
            return context.categories.FirstOrDefault(c => c.slug == slug);
        }

        public static void Add(category category)
        {
            using var context = new StoryPlatformDbContext();
            context.categories.Add(category);
            context.SaveChanges();
        }

        public static void Update(category category)
        {
            using var context = new StoryPlatformDbContext();
            context.categories.Update(category);
            context.SaveChanges();
        }

        public static void Delete(int id)
        {
            using var context = new StoryPlatformDbContext();
            var category = context.categories.FirstOrDefault(c => c.id == id);
            if (category != null)
            {
                context.categories.Remove(category);
                context.SaveChanges();
            }
        }
    }
}
