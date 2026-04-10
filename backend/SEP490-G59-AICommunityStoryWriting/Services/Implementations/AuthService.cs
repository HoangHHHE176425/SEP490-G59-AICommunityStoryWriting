using AIStory.Services.Helpers;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Repositories.Interfaces;
using Services.DTOs.Auth;
using Services.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;

namespace AIStory.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly IOtpRepository _otpRepo;      
        private readonly IEmailService _emailService;  
        private readonly ITokenService _tokenService;

        public AuthService(IUserRepository userRepo, IOtpRepository otpRepo, IEmailService emailService, ITokenService tokenService)
        {
            _userRepo = userRepo;
            _otpRepo = otpRepo;
            _emailService = emailService;
            _tokenService = tokenService;
        }

        public async Task RegisterAsync(RegisterRequest request)
        {
            ValidateRegisterRequest(request);

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
                expired_at = DateTime.UtcNow.AddMinutes(15),
                created_at = DateTime.UtcNow
            };
            await _otpRepo.AddOtp(otp);

            // 4. Gửi Email (Giả lập hoặc gọi service thật)
            await _emailService.SendEmailAsync(
                request.Email,
                "Xác thực tài khoản",
                $"Mã OTP của bạn là: <b>{otpCode}</b>. Mã có hiệu lực trong 15 phút kể từ khi bạn nhận được email này."
            );
        }

        private static void ValidateRegisterRequest(RegisterRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Email))
                throw new Exception("Email is required.");

            var emailChecker = new EmailAddressAttribute();
            if (!emailChecker.IsValid(request.Email))
                throw new Exception("Invalid Email format");

            if (string.IsNullOrWhiteSpace(request.Password))
                throw new Exception("Password is required.");

            if (request.Password.Length < 6)
                throw new Exception("Password too short");

            if (string.IsNullOrWhiteSpace(request.ConfirmPassword))
                throw new Exception("Confirm password is required.");

            if (!string.Equals(request.Password, request.ConfirmPassword, StringComparison.Ordinal))
                throw new Exception("Password not match");
        }

        public async Task<ResendOtpResponse> ResendOtpAsync(ResendOtpRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);
            if (user == null) throw new Exception("User not found.");
            if (string.Equals(user.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                throw new Exception("Account already active.");

            const int ttlSeconds = 15 * 60;
            const int ttlMinutes = 15;

            var otpCode = new Random().Next(100000, 999999).ToString();
            var otp = new otp_verifications
            {
                id = Guid.NewGuid(),
                user_id = user.id,
                otp_code = otpCode,
                type = "EMAIL_VERIFICATION",
                is_used = false,
                expired_at = DateTime.UtcNow.AddMinutes(ttlMinutes),
                created_at = DateTime.UtcNow
            };

            await _otpRepo.AddOtp(otp);
            await _emailService.SendEmailAsync(
                request.Email,
                "Xác thực tài khoản",
                $"Mã OTP của bạn là: <b>{otpCode}</b>. Mã có hiệu lực trong 15 phút kể từ khi bạn nhận được email này."
            );

            return new ResendOtpResponse
            {
                Message = "OTP mới đã được gửi. Vui lòng kiểm tra email.",
                ExpiresInSeconds = ttlSeconds
            };
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

        private static void ThrowIfAccountIsBanned(users user)
        {
            if (string.Equals(user.status, "BANNED", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("The account has been banned.");
            }
        }

        private static void EnsureAccountIsActive(users user)
        {
            ThrowIfAccountIsBanned(user);

            if (!string.Equals(user.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            {
                throw new Exception("Tài khoản của bạn chưa được xác thực. Vui lòng kiểm tra email để lấy mã OTP và xác thực tài khoản.");
            }
        }

        //  Verify OTP
        public async Task VerifyAccountAsync(VerifyOtpRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);
            if (user == null) throw new Exception("User not found.");

            if (user.status == "ACTIVE") throw new Exception("Account already active.");

            var validOtp = await _otpRepo.GetValidOtp(user.id, request.OtpCode, "EMAIL_VERIFICATION");
            if (validOtp == null)
                throw new Exception("Mã OTP không đúng hoặc đã hết hạn. Vui lòng kiểm tra lại email của bạn và thử lại (hoặc bấm \"Gửi lại OTP\").");

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

            EnsureAccountIsActive(user);

            var accessToken = _tokenService.GenerateAccessToken(user);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new NullReferenceException("Access token generation failed.");
            }

            var refreshTokenValue = _tokenService.GenerateRefreshToken();
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
            {
                throw new NullReferenceException("Refresh token generation failed.");
            }
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

        public async Task<AuthResponse> LoginWithGoogleAsync(string email, string? fullName, string googleSubject)
        {
            if (string.IsNullOrWhiteSpace(email))
                throw new ArgumentException("Email is required.", nameof(email));
            email = email.Trim();

            var user = await _userRepo.GetUserByEmail(email);

            if (user == null)
            {
                var newUserId = Guid.NewGuid();

                var baseNickname =
                    !string.IsNullOrWhiteSpace(fullName)
                        ? fullName.Trim()
                        : (email.Contains("@") ? email.Split('@')[0].Trim() : email);

                var nickname = await GenerateUniqueNicknameAsync(baseNickname, newUserId);

                // Password hash for social login (user can still be authenticated only by OAuth).
                // We store some random value to satisfy DB non-null constraint.
                var randomPassword = $"{googleSubject}_{Guid.NewGuid():N}";
                var passwordHash = BCrypt.Net.BCrypt.HashPassword(randomPassword);

                user = new users
                {
                    id = newUserId,
                    email = email,
                    password_hash = passwordHash,
                    role = "USER",
                    status = "ACTIVE",
                    email_verified_at = DateTime.UtcNow,
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

                await _userRepo.AddUser(user);
            }
            else
            {
                ThrowIfAccountIsBanned(user);

                // Make sure the account is active if Google email is verified.
                if (!string.Equals(user.status, "ACTIVE", StringComparison.OrdinalIgnoreCase))
                {
                    user.status = "ACTIVE";
                    user.email_verified_at = DateTime.UtcNow;
                    user.updated_at = DateTime.UtcNow;
                    await _userRepo.UpdateUser(user);
                }
            }

            // Generate access token
            var accessToken = _tokenService.GenerateAccessToken(user);
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new NullReferenceException("Access token generation failed.");
            }

            // Generate refresh token (stored server-side)
            var refreshTokenValue = _tokenService.GenerateRefreshToken();
            if (string.IsNullOrWhiteSpace(refreshTokenValue))
            {
                throw new NullReferenceException("Refresh token generation failed.");
            }
            var refreshToken = new auth_tokens
            {
                id = Guid.NewGuid(),
                user_id = user.id,
                refresh_token = refreshTokenValue,
                device_info = "Google",
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

            try
            {
                EnsureAccountIsActive(user);
            }
            catch
            {
                await _userRepo.DeleteRefreshToken(refreshToken);
                throw;
            }

            var newAccessToken = _tokenService.GenerateAccessToken(user);
            if (string.IsNullOrWhiteSpace(newAccessToken))
            {
                throw new NullReferenceException("Access token generation failed.");
            }

            var newRefreshTokenValue = _tokenService.GenerateRefreshToken();
            if (string.IsNullOrWhiteSpace(newRefreshTokenValue))
            {
                throw new NullReferenceException("Refresh token generation failed.");
            }

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
