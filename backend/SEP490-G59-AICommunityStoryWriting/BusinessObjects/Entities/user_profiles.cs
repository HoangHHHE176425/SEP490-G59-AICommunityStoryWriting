using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace BusinessObjects.Entities;

public partial class user_profiles
{
    public Guid user_id { get; set; }

    public string? nickname { get; set; }

    public string? avatar_url { get; set; }
    [Column("phone")]
    public string? Phone { get; set; }

    [Column("id_number")]
    public string? IdNumber { get; set; }
    public string? bio { get; set; }

    public string? description { get; set; }

    public string? social_links { get; set; }

    public string? settings { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual users user { get; set; } = null!;
}
