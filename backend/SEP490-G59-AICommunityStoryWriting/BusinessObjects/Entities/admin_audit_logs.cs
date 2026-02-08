using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class admin_audit_logs
{
    public long id { get; set; }

    public Guid? admin_id { get; set; }

    public string? action_type { get; set; }

    public string? target_type { get; set; }

    public Guid? target_id { get; set; }

    public string? old_value { get; set; }

    public string? new_value { get; set; }

    public string? reason { get; set; }

    public string? ip_address { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? admin { get; set; }
}
