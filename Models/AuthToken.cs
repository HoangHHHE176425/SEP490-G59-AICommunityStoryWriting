using System;
using System.Collections.Generic;

namespace StoryPlatform.Models;

public partial class AuthToken
{
    public string Id { get; set; } = null!;

    public string UserId { get; set; } = null!;

    public string RefreshToken { get; set; } = null!;

    public string? DeviceInfo { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
