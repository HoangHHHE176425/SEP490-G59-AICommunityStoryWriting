using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class violation_logs
{
    public Guid id { get; set; }

    public Guid? compliance_officer_id { get; set; }

    public Guid? violator_id { get; set; }

    public string? target_type { get; set; }

    public Guid? target_id { get; set; }

    public string? penalty_type { get; set; }

    public string? reason { get; set; }

    public string? policy_reference { get; set; }

    public bool? is_refunded { get; set; }

    public int? total_refunded_amount { get; set; }

    public bool? is_appealed { get; set; }

    public DateTime? created_at { get; set; }

    public virtual ICollection<appeals> appeals { get; set; } = new List<appeals>();

    public virtual users? compliance_officer { get; set; }

    public virtual users? violator { get; set; }
}
