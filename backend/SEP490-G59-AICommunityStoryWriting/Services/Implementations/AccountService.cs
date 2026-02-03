using BusinessObjects.Entities;
using Repositories.Interfaces;
using Services.DTOs.Account;
using Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Implementations
{
    public class AccountService : IAccountService
    {
        private readonly IUserRepository _userRepo;

        public AccountService(IUserRepository userRepo)
        {
            _userRepo = userRepo;
        }

        public async Task ChangePasswordAsync(int userId, ChangePasswordRequest request)
        {
            // Tìm user theo ID được truyền vào từ Token
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            {
                throw new Exception("Old password is not incorrect");
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateUser(user);
        }

        public async Task UpdateProfileAsync(int userId, UpdateProfileRequest request)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            if (user.UserProfile == null)
            {
                user.UserProfile = new UserProfile
                {
                    UserId = userId,
                    UpdatedAt = DateTime.UtcNow
                };
            }

            if (!string.IsNullOrEmpty(request.Nickname))
                user.UserProfile.Nickname = request.Nickname;

            if (!string.IsNullOrEmpty(request.Bio))
                user.UserProfile.Bio = request.Bio;

            if (!string.IsNullOrEmpty(request.AvatarUrl))
                user.UserProfile.AvatarUrl = request.AvatarUrl;

            user.UserProfile.UpdatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateUser(user);
        }
        public async Task<UserProfileResponse> GetProfileAsync(int userId)
        {
            // Tìm User trong DB
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            // Map từ Entity sang DTO để trả về
            return new UserProfileResponse
            {
                Id = user.Id,
                Email = user.Email,
                Role = user.Role,
                // Lấy thông tin từ bảng UserProfile (nếu có)
                Nickname = user.UserProfile?.Nickname,
                Bio = user.UserProfile?.Bio,
                AvatarUrl = user.UserProfile?.AvatarUrl
            };
        }
    }
}