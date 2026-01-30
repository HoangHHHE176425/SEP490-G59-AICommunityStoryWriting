using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class users
{
    public string id { get; set; } = null!;

    public string email { get; set; } = null!;

    public string password_hash { get; set; } = null!;

    public string? role { get; set; }

    public string? status { get; set; }

    public DateTime? email_verified_at { get; set; }

    public bool? must_resign_policy { get; set; }

    public DateTime? deletion_requested_at { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public bool? is_author { get; set; }

    public virtual ICollection<auth_tokens> auth_tokens { get; set; } = new List<auth_tokens>();

    public virtual ICollection<stories> stories { get; set; } = new List<stories>();

    public virtual user_profiles? user_profiles { get; set; }
}
