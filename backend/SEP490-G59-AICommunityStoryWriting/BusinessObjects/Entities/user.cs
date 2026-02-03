using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class User
{
    public int Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Role { get; set; }

    public string? Status { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public bool? MustResignPolicy { get; set; }

    public DateTime? DeletionRequestedAt { get; set; }

    public bool? IsAuthor { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AuthToken> AuthTokens { get; set; } = new List<AuthToken>();

    public virtual ICollection<Story> Stories { get; set; } = new List<Story>();

    public virtual UserProfile? UserProfile { get; set; }
}
