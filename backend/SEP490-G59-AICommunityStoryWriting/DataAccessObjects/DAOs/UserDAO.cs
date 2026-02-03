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
        private static UserDAO instance = null!;
        private static readonly object instanceLock = new object();

        private UserDAO() { }

        public static UserDAO Instance
        {
            get
            {
                lock (instanceLock)
                {
                    if (instance == null)
                    {
                        instance = new UserDAO();
                    }
                    return instance;
                }
            }
        }

        // Tìm User theo Email
        public async Task<User?> FindUserByEmail(StoryPlatformDbContext context, string email)
        {
            return await context.Users // Giả định DbSet tên là Users
                .Include(u => u.UserProfile)
                .FirstOrDefaultAsync(u => u.Email == email);
        }

        // Tìm User theo ID
        public async Task<User?> FindUserById(StoryPlatformDbContext context, int id)
        {
            return await context.Users
                .Include(u => u.UserProfile)
                .FirstOrDefaultAsync(u => u.Id == id);
        }

        // Check trùng Email
        public async Task<bool> CheckEmailExists(StoryPlatformDbContext context, string email)
        {
            return await context.Users.AnyAsync(u => u.Email == email);
        }

        // Thêm User mới
        public async Task AddUser(StoryPlatformDbContext context, User user)
        {
            context.Users.Add(user);
            await context.SaveChangesAsync();
        }

        // Cập nhật User
        public async Task UpdateUser(StoryPlatformDbContext context, User user)
        {
            context.Users.Update(user);
            await context.SaveChangesAsync();
        }

        // Thêm Refresh Token
        public async Task AddToken(StoryPlatformDbContext context, AuthToken token)
        {
            // context.AuthTokens (hoặc auth_tokens tùy tên trong DbContext)
            context.Set<AuthToken>().Add(token);
            await context.SaveChangesAsync();
        }
    }
}