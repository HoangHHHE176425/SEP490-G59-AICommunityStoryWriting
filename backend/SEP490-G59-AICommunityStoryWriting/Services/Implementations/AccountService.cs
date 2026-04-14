using AIStory.Services.Helpers;
using BusinessObjects.Account;
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
        private readonly IPolicyRepository _policyRepo;
        private readonly IAuthorPolicyAcceptanceRepository _authorPolicyAcceptanceRepo;
        private readonly JwtHelper _jwtHelper;

        public AccountService(
            IUserRepository userRepo,
            IPolicyRepository policyRepo,
            IAuthorPolicyAcceptanceRepository authorPolicyAcceptanceRepo,
            JwtHelper jwtHelper)
        {
            _userRepo = userRepo;
            _policyRepo = policyRepo;
            _authorPolicyAcceptanceRepo = authorPolicyAcceptanceRepo;
            _jwtHelper = jwtHelper;
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
            if (!await _userRepo.UserExistsAsync(userId))
            {
                throw new Exception("User not found");
            }

            var currentNickname = await _userRepo.GetUserProfileNicknameAsync(userId);

            var setNickname = false;
            string? nickname = null;
            if (!string.IsNullOrEmpty(request.DisplayName))
            {
                if (request.DisplayName != currentNickname)
                {
                    if (await _userRepo.IsNicknameExist(request.DisplayName, userId))
                    {
                        throw new Exception($"Tên hiển thị '{request.DisplayName}' đã được sử dụng. Vui lòng chọn tên khác.");
                    }

                    setNickname = true;
                    nickname = request.DisplayName;
                }
            }

            await _userRepo.PersistUserProfileAsync(userId, new UserProfilePersistModel
            {
                SetNickname = setNickname,
                Nickname = nickname,
                SetPhone = !string.IsNullOrEmpty(request.Phone),
                Phone = request.Phone,
                SetIdNumber = !string.IsNullOrEmpty(request.IdNumber),
                IdNumber = request.IdNumber,
                SetBio = request.Bio != null,
                Bio = request.Bio,
                SetDescription = request.Description != null,
                Description = request.Description,
                SetAvatarUrl = !string.IsNullOrEmpty(request.AvatarUrl),
                AvatarUrl = request.AvatarUrl
            });
        }

        public async Task<UserProfileResponse> GetProfileAsync(Guid userId)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            var (storyCount, totalReads, totalLikes) = await _userRepo.GetAuthorStoryAggregatesAsync(userId);

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

                Phone = user.user_profiles?.phone ?? "",
                IdNumber = user.user_profiles?.id_number ?? "",
                Bio = user.user_profiles?.bio ?? "",
                Description = user.user_profiles?.description ?? "",
                AvatarUrl = user.user_profiles?.avatar_url ?? "",

                JoinDate = user.created_at?.ToString("dd/MM/yyyy") ?? DateTime.UtcNow.ToString("dd/MM/yyyy"),

                IsVerified = user.status == "ACTIVE",

                Role = (user.role ?? "USER").Trim().ToUpperInvariant(),
                AuthorWritingSuspendedUntilUtc = user.author_writing_suspended_until.HasValue
                    ? (user.author_writing_suspended_until.Value.Kind == DateTimeKind.Unspecified
                        ? DateTime.SpecifyKind(user.author_writing_suspended_until.Value, DateTimeKind.Utc)
                        : user.author_writing_suspended_until.Value.ToUniversalTime())
                    : null,

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

        public async Task<AuthorOnboardingStatusResponse> GetAuthorOnboardingStatusAsync(Guid userId)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            var role = NormalizeRole(user.role);
            var activePolicy = await _policyRepo.GetActivePolicyByTypeAsync("AUTHOR");
            var acceptance = activePolicy == null
                ? null
                : await _authorPolicyAcceptanceRepo.GetAcceptanceAsync(userId, activePolicy.id);
            var hasAcceptedActivePolicy = activePolicy != null && IsAcceptanceValidForPolicy(acceptance, activePolicy);

            var isAuthor = role == "AUTHOR";
            var missingRequirements = isAuthor
                ? new List<string>()
                : GetAuthorMissingRequirements(user, role, activePolicy != null);

            return new AuthorOnboardingStatusResponse
            {
                CurrentRole = role,
                IsAuthor = isAuthor,
                HasActiveAuthorPolicy = activePolicy != null,
                ActiveAuthorPolicyId = activePolicy?.id,
                ActiveAuthorPolicyVersion = activePolicy?.version,
                HasAcceptedActivePolicy = hasAcceptedActivePolicy,
                AcceptedAt = hasAcceptedActivePolicy ? acceptance?.accepted_at : null,
                CanBecomeAuthor = !isAuthor && missingRequirements.Count == 0,
                MissingRequirements = missingRequirements
            };
        }

        public async Task<BecomeAuthorResponse> BecomeAuthorAsync(Guid userId, string? ipAddress, string? userAgent)
        {
            var user = await _userRepo.GetUserById(userId);
            if (user == null) throw new Exception("User not found");

            var role = NormalizeRole(user.role);
            if (role != "USER" && role != "AUTHOR")
            {
                throw new Exception("Chỉ tài khoản người dùng thông thường mới có thể dùng luồng đăng ký tác giả.");
            }

            var activePolicy = await _policyRepo.GetActivePolicyByTypeAsync("AUTHOR");
            if (activePolicy == null)
            {
                throw new Exception("Hiện chưa có điều khoản tác giả đang hiệu lực.");
            }

            var acceptance = await _authorPolicyAcceptanceRepo.GetAcceptanceAsync(userId, activePolicy.id);
            var missingRequirements = role == "AUTHOR"
                ? new List<string>()
                : GetAuthorMissingRequirements(user, role, hasActiveAuthorPolicy: true);

            if (missingRequirements.Count > 0)
            {
                throw new Exception("Chưa đủ điều kiện trở thành tác giả: " + string.Join("; ", missingRequirements));
            }

            var acceptedNow = false;
            var acceptedAt = acceptance?.accepted_at ?? DateTime.UtcNow;
            var hasAcceptedCurrent = IsAcceptanceValidForPolicy(acceptance, activePolicy);
            if (!hasAcceptedCurrent)
            {
                acceptedNow = true;
                acceptedAt = DateTime.UtcNow;

                if (acceptance != null)
                {
                    acceptance.accepted_at = acceptedAt;
                    acceptance.ip_address = ipAddress;
                    acceptance.user_agent = userAgent;
                    acceptance.accepted_for = "AUTHOR";
                    await _authorPolicyAcceptanceRepo.UpdateAcceptanceAsync(acceptance);
                }
                else
                {
                    var row = new author_policy_acceptances
                    {
                        id = Guid.NewGuid(),
                        user_id = userId,
                        policy_id = activePolicy.id,
                        accepted_at = acceptedAt,
                        ip_address = ipAddress,
                        user_agent = userAgent,
                        accepted_for = "AUTHOR"
                    };

                    await _authorPolicyAcceptanceRepo.AddAcceptanceAsync(row);
                }
            }

            if (role != "AUTHOR")
            {
                user.role = "AUTHOR";
                user.must_resign_policy = false;
                user.updated_at = DateTime.UtcNow;
                await _userRepo.UpdateUser(user);
            }

            return new BecomeAuthorResponse
            {
                AccessToken = _jwtHelper.GenerateToken(user),
                Role = NormalizeRole(user.role),
                PolicyId = activePolicy.id,
                AcceptedPolicyNow = acceptedNow,
                AcceptedAt = acceptedAt
            };
        }

        private static string NormalizeRole(string? role)
        {
            return string.IsNullOrWhiteSpace(role)
                ? "USER"
                : role.Trim().ToUpperInvariant();
        }

        private static List<string> GetAuthorMissingRequirements(
            users user,
            string normalizedRole,
            bool hasActiveAuthorPolicy)
        {
            var missing = new List<string>();

            if (normalizedRole != "USER")
            {
                missing.Add("Tài khoản hiện tại không thuộc nhóm USER để tự nâng cấp lên AUTHOR.");
                return missing;
            }

            if (!string.Equals(user.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                missing.Add("Tài khoản chưa ở trạng thái ACTIVE.");

            if (string.IsNullOrWhiteSpace(user.user_profiles?.nickname))
                missing.Add("Thiếu tên hiển thị.");

            if (string.IsNullOrWhiteSpace(user.user_profiles?.phone))
                missing.Add("Thiếu số điện thoại.");

            if (string.IsNullOrWhiteSpace(user.user_profiles?.id_number))
                missing.Add("Thiếu số CCCD/CMND.");

            if (!hasActiveAuthorPolicy)
                missing.Add("Chưa có điều khoản tác giả đang hiệu lực.");

            return missing;
        }

        private static bool IsAcceptanceValidForPolicy(author_policy_acceptances? acceptance, system_policies policy)
        {
            if (acceptance == null) return false;
            var effectiveFrom = policy.activated_at ?? policy.created_at ?? DateTime.MinValue;
            return acceptance.accepted_at >= effectiveFrom;
        }
    }
}