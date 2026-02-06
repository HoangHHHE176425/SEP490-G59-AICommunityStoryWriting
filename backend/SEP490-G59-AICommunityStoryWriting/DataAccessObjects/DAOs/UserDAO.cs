using BusinessObjects.Entities;
using BusinessObjects.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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

        public async Task<User?> FindUserByEmail(StoryPlatformDbContext context, string email)
        {
            return await context.Users.Include(u => u.UserProfile)
                                      .FirstOrDefaultAsync(u => u.Email == email);
        }

        public async Task<User?> FindUserById(StoryPlatformDbContext context, Guid id)
        {
            return await context.Users
                .Include(u => u.UserProfile)
                .Include(u => u.Stories) // Include truyện để đếm view, like, số lượng
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        public async Task<bool> CheckEmailExists(StoryPlatformDbContext context, string email)
        {
            return await context.Users.AnyAsync(u => u.Email == email);
        }

        public async Task AddUser(StoryPlatformDbContext context, User user)
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        public async Task UpdateUser(StoryPlatformDbContext context, User user)
        {
            context.Users.Update(user);
            await context.SaveChangesAsync();
        }

        public async Task AddToken(StoryPlatformDbContext context, AuthToken token)
        {
            context.AuthTokens.Add(token);
            await context.SaveChangesAsync();
        }
        public async Task<bool> IsNicknameExist(StoryPlatformDbContext context, string nickname, Guid currentUserId)
        {
            return await context.UserProfiles
                .AnyAsync(p => p.Nickname == nickname && p.UserId != currentUserId);
        }
        public async Task SoftDeleteUser(StoryPlatformDbContext context, Guid userId)
        {
            var user = await context.Users.FindAsync(userId);
            if (user != null)
            {

                user.Status = "DELETED";


                user.Email = $"deleted_{Guid.NewGuid()}@deleted.store";


                user.PasswordHash = "DELETED_USER_" + Guid.NewGuid().ToString();
                // ------------------------

                // 3. Cập nhật ngày
                user.UpdatedAt = DateTime.UtcNow;

                context.Users.Update(user);
                await context.SaveChangesAsync();
            }
        }
    }
}