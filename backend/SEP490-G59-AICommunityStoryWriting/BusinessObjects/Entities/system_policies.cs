using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class system_policies
{
    public Guid id { get; set; }

    public string? type { get; set; }

    public string version { get; set; } = null!;

    public string content { get; set; } = null!;

    public bool? is_active { get; set; }

    public bool? require_resign { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? activated_at { get; set; }

    public virtual ICollection<author_policy_acceptances> author_policy_acceptances { get; set; } = new List<author_policy_acceptances>();
}
