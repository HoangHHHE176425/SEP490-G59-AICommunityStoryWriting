using System;
using System.Collections.Generic;

namespace StoryPlatform.Models;

public partial class UserProfile
{
    public string UserId { get; set; } = null!;

    public string? Nickname { get; set; }

    public string? AvatarUrl { get; set; }

    public string? Bio { get; set; }

    public string? Description { get; set; }

    public string? SocialLinks { get; set; }

    public string? Settings { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
