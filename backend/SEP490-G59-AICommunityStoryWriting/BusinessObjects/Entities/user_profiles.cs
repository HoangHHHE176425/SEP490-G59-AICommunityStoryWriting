using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class user_profiles
{
    public string user_id { get; set; } = null!;

    public string? nickname { get; set; }

    public string? avatar_url { get; set; }

    public string? bio { get; set; }

    public string? description { get; set; }

    public string? social_links { get; set; }

    public string? settings { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual users user { get; set; } = null!;
}
