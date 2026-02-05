using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class reports
{
    public Guid id { get; set; }

    public Guid? reporter_id { get; set; }

    public string? target_type { get; set; }

    public Guid target_id { get; set; }

    public string? reason_category { get; set; }

    public string? description { get; set; }

    public string? status { get; set; }

    public Guid? assigned_to { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? resolved_at { get; set; }

    public virtual users? assigned_toNavigation { get; set; }

    public virtual ICollection<report_evidences> report_evidences { get; set; } = new List<report_evidences>();

    public virtual users? reporter { get; set; }
}
