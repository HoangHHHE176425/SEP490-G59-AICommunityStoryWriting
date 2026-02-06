using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessObjects.Entities;

public partial class UserProfile
{
    public Guid UserId { get; set; }

    public string? Nickname { get; set; }

    public string? AvatarUrl { get; set; }
    public string? Phone { get; set; }

    public string? IdNumber { get; set; }

    public string? Bio { get; set; }

    public string? Description { get; set; }

    public string? SocialLinks { get; set; }

    public string? Settings { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
