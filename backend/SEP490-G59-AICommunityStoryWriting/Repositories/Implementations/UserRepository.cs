using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using DataAccessObjects.Queries;
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

        public async Task<users?> GetUserByEmail(string email)
            => await UserDAO.Instance.FindUserByEmail(_context, email);

        public async Task<users?> GetUserById(Guid id)
            => await UserDAO.Instance.FindUserById(_context, id);

        public async Task<bool> IsEmailExist(string email)
            => await UserDAO.Instance.CheckEmailExists(_context, email);

        public async Task AddUser(users user)
            => await UserDAO.Instance.AddUser(_context, user);

        public async Task UpdateUser(users user)
            => await UserDAO.Instance.UpdateUser(_context, user);
        public async Task<bool> IsNicknameExist(string nickname, Guid currentUserId)
        {
            return await UserDAO.Instance.IsNicknameExist(_context, nickname, currentUserId);
        }
        public Task DeleteUser(Guid userId)
         => UserDAO.Instance.SoftDeleteUser(_context, userId);
        public async Task AddRefreshToken(auth_tokens token)
            => await UserDAO.Instance.AddToken(_context, token);

        public async Task<auth_tokens?> GetRefreshToken(string refreshToken)
            => await UserDAO.Instance.GetRefreshToken(_context, refreshToken);

        public async Task DeleteRefreshToken(string refreshToken)
            => await UserDAO.Instance.DeleteRefreshToken(_context, refreshToken);

        public Task<(IEnumerable<users> Items, int TotalCount)> GetUsersAsync(AdminUserQuery query)
            => UserDAO.Instance.GetUsersAsync(_context, query);

        public Task<(int Total, int Active, int Inactive, int Banned, int Pending, int Authors, int Moderators)> GetStatsAsync()
            => UserDAO.Instance.GetStatsAsync(_context);
    }
}