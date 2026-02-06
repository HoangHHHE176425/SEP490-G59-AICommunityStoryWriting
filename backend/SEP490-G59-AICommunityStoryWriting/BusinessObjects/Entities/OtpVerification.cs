using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class OtpVerification
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string OtpCode { get; set; } = null!;

    public string? Type { get; set; }

    public bool? IsUsed { get; set; }

    public DateTime ExpiredAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
