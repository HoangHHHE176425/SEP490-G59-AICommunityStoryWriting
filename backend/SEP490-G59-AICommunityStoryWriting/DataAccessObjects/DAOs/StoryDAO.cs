using BusinessObjects.DataAccessObjects.Context;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class StoryDAO
    {
        public static IQueryable<story> GetAll()
        {
            var context = new StoryPlatformDbContext();
            return context.stories.AsNoTracking();
        }

        public static story? GetById(int id)
        {
            using var context = new StoryPlatformDbContext();
            return context.stories.FirstOrDefault(s => s.id == id);
        }

        public static void Add(story story)
        {
            using var context = new StoryPlatformDbContext();
            context.stories.Add(story);
            context.SaveChanges();
        }

        public static void Update(story story)
        {
            using var context = new StoryPlatformDbContext();
            context.stories.Update(story);
            context.SaveChanges();
        }

        public static void Delete(int id)
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
