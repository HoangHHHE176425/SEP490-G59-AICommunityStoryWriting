using BusinessObjects.Account;
using BusinessObjects.Entities;
using DataAccessObjects.Queries;

namespace Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<users?> GetUserByEmail(string email);
        Task<users?> GetUserById(Guid id);
        Task<bool> UserExistsAsync(Guid userId);
        Task<string?> GetUserProfileNicknameAsync(Guid userId);
        Task PersistUserProfileAsync(Guid userId, UserProfilePersistModel model);
        Task<(int StoryCount, long TotalViews, int TotalFavorites)> GetAuthorStoryAggregatesAsync(Guid authorId);
        Task<bool> IsEmailExist(string email);
        Task AddUser(users user);
        Task UpdateUser(users user);
        Task DeleteUser(Guid userId);
        Task<bool> IsNicknameExist(string nickname, Guid currentUserId);
        Task AddRefreshToken(auth_tokens token);
        Task<auth_tokens?> GetRefreshToken(string refreshToken);
        Task DeleteRefreshToken(string refreshToken);
        Task DeleteRefreshTokensByUserId(Guid userId);
        Task<int> MarkAuthorsMustResignPolicyAsync(Guid activeAuthorPolicyId);
        Task<int> ClearAuthorMustResignPolicyFlagAsync();

        // Admin
        Task<(IEnumerable<users> Items, int TotalCount)> GetUsersAsync(AdminUserQuery query);
        Task<(int Total, int Active, int Inactive, int Banned, int Pending, int Authors, int Moderators)> GetStatsAsync();
    }
}