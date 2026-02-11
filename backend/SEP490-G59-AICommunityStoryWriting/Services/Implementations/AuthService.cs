using AIStory.Services.Helpers;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Repositories.Interfaces;
using Services.DTOs.Auth;
using Services.Interfaces;
using System.Security.Cryptography;

namespace AIStory.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IOtpRepository _otpRepo;      
        private readonly IEmailService _emailService;  
        private readonly JwtHelper _jwtHelper;

        public AuthService(IUserRepository userRepo, IOtpRepository otpRepo, IEmailService emailService, JwtHelper jwtHelper)
        {
            _userRepo = userRepo;
            _otpRepo = otpRepo;
            _emailService = emailService;
            _jwtHelper = jwtHelper;
        }

        public async Task RegisterAsync(RegisterRequest request)
        {
            if (await _userRepo.IsEmailExist(request.Email))
                throw new Exception("Email already exists.");

            var newUserId = Guid.NewGuid();
            var baseNickname =
                !string.IsNullOrWhiteSpace(request.FullName)
                    ? request.FullName.Trim()
                    : request.Email.Split('@')[0].Trim();

            var nickname = await GenerateUniqueNicknameAsync(baseNickname, newUserId);

            var newUser = new users
            {
                id = newUserId,
                email = request.Email,
                password_hash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                role = "USER",
                status = "PENDING", 
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow,
                user_profiles = new user_profiles
                {
                    user_id = newUserId,
                    nickname = nickname,
                    settings = "{\"allow_notif\":true}",
                    updated_at = DateTime.UtcNow
                }
            };

            await _userRepo.AddUser(newUser);

            // 3. Tạo OTP
            var otpCode = new Random().Next(100000, 999999).ToString();
            var otp = new otp_verifications
            {
                id = Guid.NewGuid(),
                user_id = newUserId,
                otp_code = otpCode,
                type = "EMAIL_VERIFICATION",
                is_used = false,
                expired_at = DateTime.UtcNow.AddMinutes(5),
                created_at = DateTime.UtcNow
            };
            await _otpRepo.AddOtp(otp);

            // 4. Gửi Email (Giả lập hoặc gọi service thật)
            await _emailService.SendEmailAsync(request.Email, "Xác thực tài khoản", $"Mã OTP của bạn: {otpCode}");
        }

        private async Task<string> GenerateUniqueNicknameAsync(string baseNickname, Guid currentUserId)
        {
            var nick = (baseNickname ?? "").Trim();
            if (string.IsNullOrWhiteSpace(nick))
            {
                nick = "user";
            }

            // DB constraint: nickname max length 100
            if (nick.Length > 100) nick = nick.Substring(0, 100);

            // If not taken, use it directly.
            if (!await _userRepo.IsNicknameExist(nick, currentUserId))
            {
                return nick;
            }

            // Try appending a short suffix.
            for (var i = 0; i < 5; i++)
            {
                var suffix = new Random().Next(1000, 9999).ToString();
                var candidateBase = nick;
                var maxBaseLen = 100 - (1 + suffix.Length);
                if (candidateBase.Length > maxBaseLen)
                {
                    candidateBase = candidateBase.Substring(0, maxBaseLen);
                }
                var candidate = $"{candidateBase}_{suffix}";
                if (!await _userRepo.IsNicknameExist(candidate, currentUserId))
                {
                    return candidate;
                }
            }

            // Fall back to GUID suffix (still capped to 100).
            var guidSuffix = currentUserId.ToString("N").Substring(0, 8);
            var maxLen = 100 - (1 + guidSuffix.Length);
            var trimmed = nick.Length > maxLen ? nick.Substring(0, maxLen) : nick;
            return $"{trimmed}_{guidSuffix}";
        }

        //  Verify OTP
        public async Task VerifyAccountAsync(VerifyOtpRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);
            if (user == null) throw new Exception("User not found.");

            if (user.status == "ACTIVE") throw new Exception("Account already active.");

            var validOtp = await _otpRepo.GetValidOtp(user.id, request.OtpCode, "EMAIL_VERIFICATION");
            if (validOtp == null) throw new Exception("Invalid or expired OTP.");

            user.status = "ACTIVE";
            user.email_verified_at = DateTime.UtcNow;
            await _userRepo.UpdateUser(user);

            // Hủy OTP
            await _otpRepo.MarkOtpAsUsed(validOtp.id);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.password_hash))
            {
                throw new Exception("Invalid email or password.");
            }

            // CHECK TRẠNG THÁI
            if (user.status != "ACTIVE")
            {
                throw new Exception("Account is not verified. Please check email for OTP.");
            }

            var accessToken = _jwtHelper.GenerateToken(user);
            var refreshTokenValue = GenerateRefreshToken();
            var refreshToken = new auth_tokens
            {
                id = Guid.NewGuid(), 
                user_id = user.id,    
                refresh_token = refreshTokenValue,
                device_info = "Unknown",
                expires_at = DateTime.UtcNow.AddDays(30),
                created_at = DateTime.UtcNow
            };

            await _userRepo.AddRefreshToken(refreshToken);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.refresh_token
            };
        }

        public async Task<AuthResponse> RefreshAsync(string refreshToken)
        {
            var tokenRow = await _userRepo.GetRefreshToken(refreshToken);
            if (tokenRow == null || tokenRow.user_id == null)
            {
                throw new Exception("Invalid refresh token.");
            }

            // rotate refresh token
            var user = await _userRepo.GetUserById(tokenRow.user_id.Value);
            if (user == null)
            {
                throw new Exception("User not found.");
            }

            var newAccessToken = _jwtHelper.GenerateToken(user);
            var newRefreshTokenValue = GenerateRefreshToken();

            var newRow = new auth_tokens
            {
                id = Guid.NewGuid(),
                user_id = user.id,
                refresh_token = newRefreshTokenValue,
                device_info = tokenRow.device_info,
                expires_at = DateTime.UtcNow.AddDays(30),
                created_at = DateTime.UtcNow
            };

            await _userRepo.AddRefreshToken(newRow);
            await _userRepo.DeleteRefreshToken(refreshToken);

            return new AuthResponse
            {
                AccessToken = newAccessToken,
                RefreshToken = newRefreshTokenValue
            };
        }

        public async Task LogoutAsync(string refreshToken)
        {
            await _userRepo.DeleteRefreshToken(refreshToken);
        }

        private static string GenerateRefreshToken()
        {
            // 64 bytes -> 86 chars base64url (approx), good entropy
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }


        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);
            if (user == null) throw new Exception("Người dùng không tồn tại.");

            var validOtp = await _otpRepo.GetValidOtp(user.id, request.OtpCode, "RESET_PASSWORD");

            if (validOtp == null)
            {
                throw new Exception("Mã xác thực không đúng hoặc đã hết hạn.");
            }


            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.password_hash = passwordHash; 
            user.updated_at = DateTime.UtcNow;

            await _userRepo.UpdateUser(user);

            await _otpRepo.MarkOtpAsUsed(validOtp.id);
        }
        public async Task ForgotPasswordAsync(ForgotPasswordRequest request)
        {
            // 1. Lấy email từ request DTO
            var user = await _userRepo.GetUserByEmail(request.Email);

            // Nếu user không tồn tại -> Return luôn (Security)
            if (user == null)
            {
                return;
            }

            // 2. Tạo OTP
            var otpCode = new Random().Next(100000, 999999).ToString();

            var otp = new otp_verifications
            {
                id = Guid.NewGuid(),
                user_id = user.id,
                otp_code = otpCode,
                type = "RESET_PASSWORD",
                is_used = false,
                expired_at = DateTime.UtcNow.AddMinutes(15),
                created_at = DateTime.UtcNow
            };

            // 3. Lưu OTP
            await _otpRepo.AddOtp(otp);

            // 4. Gửi Email (Sửa lỗi biến 'email' thành 'request.Email')
            await _emailService.SendEmailAsync(request.Email, "Đặt lại mật khẩu",
                $"Mã xác thực của bạn là: <b>{otpCode}</b>. Mã có hiệu lực trong 15 phút.");
        }
    }
}
