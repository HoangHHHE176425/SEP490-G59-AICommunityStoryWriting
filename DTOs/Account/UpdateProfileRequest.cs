namespace StoryPlatform.DTOs.Account;

public class UpdateProfileRequest
{
    public string? Nickname { get; set; }
    public string? Bio { get; set; }
    public string? AvatarUrl { get; set; }
}
