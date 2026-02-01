using Microsoft.EntityFrameworkCore;
using StoryPlatform.DTOs.Auth;
using StoryPlatform.Helpers;
using StoryPlatform.Models;
using BCrypt.Net;

public class AuthService : IAuthService
{
    private readonly StoryPlatformContext _context;
    private readonly JwtHelper _jwt;

    public AuthService(StoryPlatformContext context, JwtHelper jwt)
    {
        _context = context;
        _jwt = jwt;
    }

    public async Task RegisterAsync(RegisterRequest request)
    {
        if (await _context.Users.AnyAsync(x => x.Email == request.Email))
            throw new Exception("Email already exists");

        var user = new User
        {
            Id = Guid.NewGuid().ToString(),
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = "USER",
            Status = "ACTIVE",
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);

        _context.UserProfiles.Add(new UserProfile
        {
            UserId = user.Id,
            Nickname = request.Email.Split('@')[0]
        });

        await _context.SaveChangesAsync();
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var user = await _context.Users
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Email == request.Email);

        if (user == null)
            throw new Exception("User not found");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new Exception("Invalid password");

        var accessToken = _jwt.GenerateToken(user);

        var refreshToken = new AuthToken
        {
            Id = Guid.NewGuid().ToString(),
            UserId = user.Id,
            RefreshToken = Guid.NewGuid().ToString(),
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow
        };

        _context.AuthTokens.Add(refreshToken);
        await _context.SaveChangesAsync();

        return new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.RefreshToken
        };
    }
}
