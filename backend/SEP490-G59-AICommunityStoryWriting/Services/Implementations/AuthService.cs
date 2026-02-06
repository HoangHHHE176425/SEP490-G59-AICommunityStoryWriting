using AIStory.Services.Helpers;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Repositories.Interfaces;
using Services.DTOs.Auth;
using Services.Interfaces;

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
            var newUser = new User
            {
                Id = newUserId,
                Email = request.Email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = "USER",
                Status = "PENDING", 
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                UserProfile = new UserProfile
                {
                    UserId = newUserId,
                    Nickname = request.FullName ?? request.Email.Split('@')[0],
                    Settings = "{\"allow_notif\":true}",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await _userRepo.AddUser(newUser);

            // 3. Tạo OTP
            var otpCode = new Random().Next(100000, 999999).ToString();
            var otp = new OtpVerification
            {
                Id = Guid.NewGuid(),
                UserId = newUserId,
                OtpCode = otpCode,
                Type = "EMAIL_VERIFICATION",
                IsUsed = false,
                ExpiredAt = DateTime.UtcNow.AddMinutes(5),
                CreatedAt = DateTime.UtcNow
            };
            await _otpRepo.AddOtp(otp);

            // 4. Gửi Email (Giả lập hoặc gọi service thật)
            await _emailService.SendEmailAsync(request.Email, "Xác thực tài khoản", $"Mã OTP của bạn: {otpCode}");
        }

        //  Verify OTP
        public async Task VerifyAccountAsync(VerifyOtpRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);
            if (user == null) throw new Exception("User not found.");

            if (user.Status == "ACTIVE") throw new Exception("Account already active.");

            var validOtp = await _otpRepo.GetValidOtp(user.Id, request.OtpCode, "EMAIL_VERIFICATION");
            if (validOtp == null) throw new Exception("Invalid or expired OTP.");

            user.Status = "ACTIVE";
            user.EmailVerifiedAt = DateTime.UtcNow;
            await _userRepo.UpdateUser(user);

            // Hủy OTP
            await _otpRepo.MarkOtpAsUsed(validOtp.Id);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);

            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new Exception("Invalid email or password.");
            }

            // CHECK TRẠNG THÁI
            if (user.Status != "ACTIVE")
            {
                throw new Exception("Account is not verified. Please check email for OTP.");
            }

            var accessToken = _jwtHelper.GenerateToken(user);
            var refreshToken = new AuthToken
            {
                Id = Guid.NewGuid(), 
                UserId = user.Id,    
                RefreshToken = Guid.NewGuid().ToString(),
                DeviceInfo = "Unknown",
                ExpiresAt = DateTime.UtcNow.AddDays(30),
                CreatedAt = DateTime.UtcNow
            };

            await _userRepo.AddRefreshToken(refreshToken);

            return new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken.RefreshToken
            };
        }


        public async Task ResetPasswordAsync(ResetPasswordRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);
            if (user == null) throw new Exception("Người dùng không tồn tại.");

            var validOtp = await _otpRepo.GetValidOtp(user.Id, request.OtpCode, "RESET_PASSWORD");

            if (validOtp == null)
            {
                throw new Exception("Mã xác thực không đúng hoặc đã hết hạn.");
            }


            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
            user.PasswordHash = passwordHash; 
            user.UpdatedAt = DateTime.UtcNow;

            await _userRepo.UpdateUser(user);

            await _otpRepo.MarkOtpAsUsed(validOtp.Id);
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

            var otp = new OtpVerification
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                OtpCode = otpCode,
                Type = "RESET_PASSWORD",
                IsUsed = false,
                ExpiredAt = DateTime.UtcNow.AddMinutes(15),
                CreatedAt = DateTime.UtcNow
            };

            // 3. Lưu OTP
            await _otpRepo.AddOtp(otp);

            // 4. Gửi Email (Sửa lỗi biến 'email' thành 'request.Email')
            await _emailService.SendEmailAsync(request.Email, "Đặt lại mật khẩu",
                $"Mã xác thực của bạn là: <b>{otpCode}</b>. Mã có hiệu lực trong 15 phút.");
        }
    }
}
