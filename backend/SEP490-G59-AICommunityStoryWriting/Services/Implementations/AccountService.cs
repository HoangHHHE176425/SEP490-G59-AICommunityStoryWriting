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


            bool isPasswordCorrect = BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.password_hash);

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

            user.password_hash = newPasswordHash;
            user.updated_at = DateTime.UtcNow;

            // 5. Gọi Repository để update
            await _userRepo.UpdateUser(user);
        }

        public async Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            if (user.user_profiles == null)
            {
                user.user_profiles = new user_profiles
                {
                    user_id = userId,
                    social_links = "{}",
                    settings = "{\"allow_notif\": true}"
                };
            }

            //  KIỂM TRA TRÙNG NICKNAME
            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                // Chỉ check nếu nickname thay đổi so với cái cũ
                if (request.DisplayName != user.user_profiles.nickname)
                {
                    bool isExist = await _userRepo.IsNicknameExist(request.DisplayName, userId);
                    if (isExist)
                    {
                        throw new Exception($"Tên hiển thị '{request.DisplayName}' đã được sử dụng. Vui lòng chọn tên khác.");
                    }
                    user.user_profiles.nickname = request.DisplayName;
                }
            }

            if (!string.IsNullOrEmpty(request.Phone)) user.user_profiles.Phone = request.Phone;
            if (!string.IsNullOrEmpty(request.IdNumber)) user.user_profiles.IdNumber = request.IdNumber;
            if (request.Bio != null) user.user_profiles.bio = request.Bio;
            if (request.Description != null) user.user_profiles.description = request.Description;
            if (!string.IsNullOrEmpty(request.AvatarUrl)) user.user_profiles.avatar_url = request.AvatarUrl;

            user.user_profiles.updated_at = DateTime.UtcNow;
            user.updated_at = DateTime.UtcNow;

            await _userRepo.UpdateUser(user);
        }

        public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            int storyCount = user.stories?.Count ?? 0;
            long totalReads = user.stories?.Sum(s => s.total_views ?? 0) ?? 0;
            int totalLikes = user.stories?.Sum(s => s.total_favorites ?? 0) ?? 0;

            // 2. Tạo Tags (Giả lập logic hiển thị)
            var tags = new List<string>();
            if (storyCount > 0) tags.Add("Tác giả");
            if (user.role == "ADMIN") tags.Add("Quản trị viên");
            if (totalReads > 1000) tags.Add("Cây bút vàng");
            if (tags.Count == 0) tags.Add("Thành viên mới");

            return new UserProfileResponse
            {
                Id = user.id,
                Email = user.email,

                DisplayName = !string.IsNullOrEmpty(user.user_profiles?.nickname)
                              ? user.user_profiles.nickname
                              : user.email.Split('@')[0],

                Phone = user.user_profiles?.Phone ?? "",
                IdNumber = user.user_profiles?.IdNumber ?? "",
                Bio = user.user_profiles?.bio ?? "",
                Description = user.user_profiles?.description ?? "",
                AvatarUrl = user.user_profiles?.avatar_url ?? "",

                JoinDate = user.created_at?.ToString("dd/MM/yyyy") ?? DateTime.UtcNow.ToString("dd/MM/yyyy"),

                IsVerified = user.status == "ACTIVE",

                Role = (user.role ?? "USER").Trim().ToUpperInvariant(),

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