using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class UserDAO
    {
        private static UserDAO instance = null;
        private static readonly object instanceLock = new object();
        public static UserDAO Instance
        {
            get { lock (instanceLock) { return instance ??= new UserDAO(); } }
        }

        public async Task<users?> FindUserByEmail(StoryPlatformDbContext context, string email)
        {
            return await context.users.Include(u => u.user_profiles)
                                      .FirstOrDefaultAsync(u => u.email == email);
        }

        public async Task<users?> FindUserById(StoryPlatformDbContext context, Guid id)
        {
            return await context.users
                .Include(u => u.user_profiles)
                .Include(u => u.stories) // Include truyện để đếm view, like, số lượng
                .FirstOrDefaultAsync(u => u.id == id);
        }

        public async Task<bool> CheckEmailExists(StoryPlatformDbContext context, string email)
        {
            return await context.users.AnyAsync(u => u.email == email);
        }

        public async Task AddUser(StoryPlatformDbContext context, users user)
        {
            context.users.Add(user);
            await context.SaveChangesAsync();
        }

        public async Task UpdateUser(StoryPlatformDbContext context, users user)
        {
            context.users.Update(user);
            await context.SaveChangesAsync();
        }

        public async Task AddToken(StoryPlatformDbContext context, auth_tokens token)
        {
            context.auth_tokens.Add(token);
            await context.SaveChangesAsync();
        }
        public async Task<bool> IsNicknameExist(StoryPlatformDbContext context, string nickname, Guid currentUserId)
        {
            return await context.user_profiles
                .AnyAsync(p => p.nickname == nickname && p.user_id != currentUserId);
        }
        public async Task SoftDeleteUser(StoryPlatformDbContext context, Guid userId)
        {
            var user = await context.users.FindAsync(userId);
            if (user != null)
            {

                user.status = "DELETED";


                user.email = $"deleted_{Guid.NewGuid()}@deleted.store";


                user.password_hash = "DELETED_USER_" + Guid.NewGuid().ToString();

                user.updated_at = DateTime.UtcNow;

                context.users.Update(user);
                await context.SaveChangesAsync();
            }
        }
        public static bool Exists(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.users.Any(u => u.id == id);
        }
    }
}
