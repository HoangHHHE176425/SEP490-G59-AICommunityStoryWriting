using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class otp_verifications
{
    public Guid id { get; set; }

    public Guid? user_id { get; set; }

    public string otp_code { get; set; } = null!;

    public string? type { get; set; }

    public bool? is_used { get; set; }

    public DateTime expired_at { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? user { get; set; }
}
