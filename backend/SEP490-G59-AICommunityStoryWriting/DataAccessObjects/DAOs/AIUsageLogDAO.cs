using BusinessObjects;
using BusinessObjects.Entities;

namespace DataAccessObjects.DAOs
{
    public static class AIUsageLogDAO
    {
        public static void Add(ai_usage_logs log)
        {
            using var context = new StoryPlatformDbContext();
            context.ai_usage_logs.Add(log);
            context.SaveChanges();
        }
    }
}
