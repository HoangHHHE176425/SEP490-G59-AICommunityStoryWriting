using BusinessObjects.Entities;
using BusinessObjects.Models;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class UserRepository : IUserRepository
    {
        private readonly StoryPlatformDbContext _context;

        public UserRepository(StoryPlatformDbContext context)
        {
            _context = context;
        }

        public async Task<User?> GetUserByEmail(string email)
            => await UserDAO.Instance.FindUserByEmail(_context, email);

        public async Task<User?> GetUserById(Guid id)
            => await UserDAO.Instance.FindUserById(_context, id);

        public async Task<bool> IsEmailExist(string email)
            => await UserDAO.Instance.CheckEmailExists(_context, email);

        public async Task AddUser(User user)
            => await UserDAO.Instance.AddUser(_context, user);

        public async Task UpdateUser(User user)
            => await UserDAO.Instance.UpdateUser(_context, user);
        public async Task<bool> IsNicknameExist(string nickname, Guid currentUserId)
        {
            return await UserDAO.Instance.IsNicknameExist(_context, nickname, currentUserId);
        }
        public Task DeleteUser(Guid userId)
         => UserDAO.Instance.SoftDeleteUser(_context, userId);
        public async Task AddRefreshToken(AuthToken token)
            => await UserDAO.Instance.AddToken(_context, token);
    }
}