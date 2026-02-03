using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AuthToken
{
    public int Id { get; set; }

    public int UserId { get; set; }

    public string RefreshToken { get; set; } = null!;

    public string? DeviceInfo { get; set; }

    public DateTime ExpiresAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
