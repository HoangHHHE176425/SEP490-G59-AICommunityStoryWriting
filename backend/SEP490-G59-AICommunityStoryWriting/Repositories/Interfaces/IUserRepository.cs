using BusinessObjects.Entities;

namespace Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetUserByEmail(string email);
        Task<User?> GetUserById(Guid id); 
        Task<bool> IsEmailExist(string email);
        Task AddUser(User user);
        Task UpdateUser(User user);
        Task DeleteUser(Guid userId);
        Task<bool> IsNicknameExist(string nickname, Guid currentUserId);
        Task AddRefreshToken(AuthToken token);
    }
}