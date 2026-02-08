using BusinessObjects.Entities;

namespace Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<users?> GetUserByEmail(string email);
        Task<users?> GetUserById(Guid id); 
        Task<bool> IsEmailExist(string email);
        Task AddUser(users user);
        Task UpdateUser(users user);
        Task DeleteUser(Guid userId);
        Task<bool> IsNicknameExist(string nickname, Guid currentUserId);
        Task AddRefreshToken(auth_tokens token);
        Task<auth_tokens?> GetRefreshToken(string refreshToken);
        Task DeleteRefreshToken(string refreshToken);
    }
}