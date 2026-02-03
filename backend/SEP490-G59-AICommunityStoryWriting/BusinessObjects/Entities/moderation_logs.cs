using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class moderation_logs
{
    public long id { get; set; }

    public Guid? moderator_id { get; set; }

    public string? target_type { get; set; }

    public Guid? target_id { get; set; }

    public string? action { get; set; }

    public string? rejection_reason { get; set; }

    public int? processing_time_ms { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? moderator { get; set; }
}
