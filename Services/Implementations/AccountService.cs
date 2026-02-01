using Microsoft.EntityFrameworkCore;
using StoryPlatform.DTOs.Account;
using StoryPlatform.Models;

public class AccountService : IAccountService
{
    private readonly StoryPlatformContext _context;

    public AccountService(StoryPlatformContext context)
    {
        _context = context;
    }

    public async Task<object> GetProfileAsync(string userId)
    {
        var user = await _context.Users
            .Include(x => x.UserProfile)
            .FirstOrDefaultAsync(x => x.Id == userId);

        if (user == null) throw new Exception("User not found");

        return new
        {
            user.Id,
            user.Email,
            user.Role,
            user.Status,
            Profile = user.UserProfile
        };
    }

    public async Task UpdateProfileAsync(string userId, UpdateProfileRequest request)
    {
        var profile = await _context.UserProfiles
            .FirstOrDefaultAsync(x => x.UserId == userId);

        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
        }

        profile.Nickname = request.Nickname;
        profile.Bio = request.Bio;
        profile.AvatarUrl = request.AvatarUrl;
        profile.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }

    public async Task ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        var user = await _context.Users.FirstOrDefaultAsync(x => x.Id == userId);

        if (!BCrypt.Net.BCrypt.Verify(request.OldPassword, user.PasswordHash))
            throw new Exception("Old password incorrect");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}
