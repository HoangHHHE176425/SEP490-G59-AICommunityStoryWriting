using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class appeals
{
    public Guid id { get; set; }

    public Guid? violation_id { get; set; }

    public Guid? user_id { get; set; }

    public string content { get; set; } = null!;

    public string? evidence_url { get; set; }

    public string? status { get; set; }

    public Guid? reviewed_by { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? user { get; set; }

    public virtual violation_logs? violation { get; set; }
}
