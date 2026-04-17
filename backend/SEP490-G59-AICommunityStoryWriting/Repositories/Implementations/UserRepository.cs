using BusinessObjects;
using BusinessObjects.Account;
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

        public Task<(int StoryCount, long TotalViews, int TotalFavorites)> GetAuthorStoryAggregatesAsync(Guid authorId)
            => UserDAO.Instance.GetAuthorStoryAggregatesAsync(_context, authorId);

        public Task<bool> UserExistsAsync(Guid userId)
            => UserDAO.Instance.UserExistsAsync(_context, userId);

        public Task<string?> GetUserProfileNicknameAsync(Guid userId)
            => UserDAO.Instance.GetUserProfileNicknameAsync(_context, userId);

        public Task PersistUserProfileAsync(Guid userId, UserProfilePersistModel model)
            => UserDAO.Instance.PersistUserProfileAsync(_context, userId, model);

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

        public async Task DeleteRefreshTokensByUserId(Guid userId)
            => await UserDAO.Instance.DeleteRefreshTokensByUserId(_context, userId);

        public Task<int> MarkAuthorsMustResignPolicyAsync(Guid activeAuthorPolicyId)
            => UserDAO.Instance.MarkAuthorsMustResignPolicyAsync(_context, activeAuthorPolicyId);

        public Task<int> ClearAuthorMustResignPolicyFlagAsync()
            => UserDAO.Instance.ClearAuthorMustResignPolicyFlagAsync(_context);

        public Task<(IEnumerable<users> Items, int TotalCount)> GetUsersAsync(AdminUserQuery query)
            => UserDAO.Instance.GetUsersAsync(_context, query);

        public Task<(int Total, int Active, int Inactive, int Banned, int Pending, int Authors, int Moderators)> GetStatsAsync()
            => UserDAO.Instance.GetStatsAsync(_context);

        public Task<int> SetAuthorAiTokenBudgetLimitsAsync(
            Guid userId,
            bool setLifetime,
            long? lifetimeLimit,
            bool setPerDay,
            long? perDayLimit,
            bool setPerWeek,
            long? perWeekLimit,
            bool setPerMonth,
            long? perMonthLimit,
            CancellationToken cancellationToken = default)
            => UserDAO.Instance.SetAuthorAiTokenBudgetLimitsAsync(
                _context,
                userId,
                setLifetime,
                lifetimeLimit,
                setPerDay,
                perDayLimit,
                setPerWeek,
                perWeekLimit,
                setPerMonth,
                perMonthLimit,
                cancellationToken);
    }
}