using BusinessObjects.Entities;

namespace Repositories.Interfaces
{
    public interface IUserRepository
    {
        Task<User?> GetUserByEmail(string email);
        Task<User?> GetUserById(int id);
        Task<bool> IsEmailExist(string email);
        Task AddUser(User user);
        Task UpdateUser(User user);
        Task AddRefreshToken(AuthToken token);
    }
}