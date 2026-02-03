using AIStory.Services.Helpers;
using BusinessObjects.Entities;
using Repositories.Interfaces;
using Services.Interfaces;

namespace AIStory.Services.Implementations
{
    public class AuthService : IAuthService
    {
        private readonly IUserRepository _userRepo;
        private readonly JwtHelper _jwtHelper;

        public AuthService(IUserRepository userRepo, JwtHelper jwtHelper)
        {
            _userRepo = userRepo;
            _jwtHelper = jwtHelper;
        }

        public async Task RegisterAsync(RegisterRequest request)
        {
            // Kiểm tra email trùng
            if (await _userRepo.IsEmailExist(request.Email))
            {
                throw new Exception("Email already exists.");
            }

            // Hash password
            string passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Tạo User mới (Dùng Property viết Hoa)
            var newUser = new User
            {
                Email = request.Email,
                PasswordHash = passwordHash,
                Role = "USER",
                Status = "ACTIVE",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,

                // Entity Framework sẽ tự link UserProfile này với User
                UserProfile = new UserProfile
                {
                    Nickname = request.Email.Split('@')[0],
                    Settings = "{\"allow_notif\":true}",
                    UpdatedAt = DateTime.UtcNow
                }
            };

            await _userRepo.AddUser(newUser);
        }

        public async Task<AuthResponse> LoginAsync(LoginRequest request)
        {
            var user = await _userRepo.GetUserByEmail(request.Email);

            // Dùng user.PasswordHash
            if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            {
                throw new Exception("Invalid email or password.");
            }

            var accessToken = _jwtHelper.GenerateToken(user);

            // Tạo Refresh Token
            var refreshToken = new AuthToken
            {
                UserId = user.Id, // user.Id (int)
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
    }
}