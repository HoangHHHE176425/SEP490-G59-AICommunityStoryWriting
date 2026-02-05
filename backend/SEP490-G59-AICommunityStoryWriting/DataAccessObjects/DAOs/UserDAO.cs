using BusinessObjects;
using BusinessObjects.Entities;

namespace DataAccessObjects.DAOs
{
    public class UserDAO
    {
        public static bool Exists(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.users.Any(u => u.id == id);
        }
    }
}
