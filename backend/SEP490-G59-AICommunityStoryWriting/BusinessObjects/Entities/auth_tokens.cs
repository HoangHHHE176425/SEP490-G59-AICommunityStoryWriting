using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class auth_tokens
{
    public Guid id { get; set; }

    public Guid? user_id { get; set; }

    public string refresh_token { get; set; } = null!;

    public string? device_info { get; set; }

    public DateTime expires_at { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? user { get; set; }
}
