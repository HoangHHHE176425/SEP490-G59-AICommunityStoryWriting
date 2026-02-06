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
        public async Task DeleteAccountAsync(Guid userId)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("Người dùng không tồn tại.");

            await _userRepo.DeleteUser(userId);
        }
        public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
        {
            // 1. Lấy thông tin User từ DB
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("Không tìm thấy người dùng.");


            bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash);

            if (!isPasswordCorrect)
            {
                throw new Exception("Mật khẩu hiện tại không chính xác.");
            }

            // 3. Kiểm tra trùng lặp (Optional: Không cho trùng mật khẩu cũ)
            if (request.CurrentPassword == request.NewPassword)
            {
                throw new Exception("Mật khẩu mới không được trùng với mật khẩu cũ.");
            }

            // 4. Mã hóa mật khẩu mới và lưu vào DB
            string newPasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);

            user.PasswordHash = newPasswordHash;
            user.UpdatedAt = DateTime.UtcNow;

            // 5. Gọi Repository để update
            await _userRepo.UpdateUser(user);
        }

        public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            if (user.UserProfile == null)
            {
                user.UserProfile = new UserProfile
                {
                    UserId = userId,
                    SocialLinks = "{}",
                    Settings = "{\"allow_notif\": true}"
                };
            }

            //  KIỂM TRA TRÙNG NICKNAME
            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                // Chỉ check nếu nickname thay đổi so với cái cũ
                if (request.DisplayName != user.UserProfile.Nickname)
                {
                    bool isExist = await _userRepo.IsNicknameExist(request.DisplayName, userId);
                    if (isExist)
                    {
                        throw new Exception($"Tên hiển thị '{request.DisplayName}' đã được sử dụng. Vui lòng chọn tên khác.");
                    }
                    user.UserProfile.Nickname = request.DisplayName;
                }
            }

            if (!string.IsNullOrEmpty(request.Phone)) user.UserProfile.Phone = request.Phone;
            if (!string.IsNullOrEmpty(request.IdNumber)) user.UserProfile.IdNumber = request.IdNumber;
            if (request.Bio != null) user.UserProfile.Bio = request.Bio;
            if (request.Description != null) user.UserProfile.Description = request.Description;
            if (!string.IsNullOrEmpty(request.AvatarUrl)) user.UserProfile.AvatarUrl = request.AvatarUrl;

            user.UserProfile.UpdatedAt = DateTime.UtcNow;
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateUser(user);
        }

        public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            int storyCount = user.Stories?.Count ?? 0;
            long totalReads = user.Stories?.Sum(s => s.TotalViews ?? 0) ?? 0;
            int totalLikes = user.Stories?.Sum(s => s.TotalFavorites ?? 0) ?? 0;

            // 2. Tạo Tags (Giả lập logic hiển thị)
            var tags = new List<string>();
            if (storyCount > 0) tags.Add("Tác giả");
            if (user.Role == "ADMIN") tags.Add("Quản trị viên");
            if (totalReads > 1000) tags.Add("Cây bút vàng");
            if (tags.Count == 0) tags.Add("Thành viên mới");

            return new UserProfileResponse
            {
                Id = user.Id,
                Email = user.Email,

                DisplayName = !string.IsNullOrEmpty(user.UserProfile?.Nickname)
                              ? user.UserProfile.Nickname
                              : user.Email.Split('@')[0],

                Phone = user.UserProfile?.Phone ?? "",
                IdNumber = user.UserProfile?.IdNumber ?? "",
                Bio = user.UserProfile?.Bio ?? "",
                Description = user.UserProfile?.Description ?? "",
                AvatarUrl = user.UserProfile?.AvatarUrl ?? "",

                JoinDate = user.CreatedAt?.ToString("dd/MM/yyyy") ?? DateTime.UtcNow.ToString("dd/MM/yyyy"),

                IsVerified = user.Status == "ACTIVE",

                Tags = tags,

                Stats = new UserStats
                {
                    StoriesWritten = storyCount,
                    TotalReads = totalReads,
                    Likes = totalLikes,
                    CurrentCoins = 0 // Tạm thời trả về 0 như yêu cầu
                }
            };
        }
    }
}