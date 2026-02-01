using System;
using System.Collections.Generic;

namespace StoryPlatform.Models;

public partial class User
{
    public string Id { get; set; } = null!;

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Role { get; set; }

    public string? Status { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public bool? MustResignPolicy { get; set; }

    public DateTime? DeletionRequestedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public bool? IsAuthor { get; set; }

    public virtual ICollection<AuthToken> AuthTokens { get; set; } = new List<AuthToken>();

    public virtual ICollection<Story> Stories { get; set; } = new List<Story>();

    public virtual UserProfile? UserProfile { get; set; }
}
