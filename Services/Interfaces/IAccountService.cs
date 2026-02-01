using StoryPlatform.DTOs.Account;

public interface IAccountService
{
    Task<object> GetProfileAsync(string userId);
    Task UpdateProfileAsync(string userId, UpdateProfileRequest request);
    Task ChangePasswordAsync(string userId, ChangePasswordRequest request);
}
