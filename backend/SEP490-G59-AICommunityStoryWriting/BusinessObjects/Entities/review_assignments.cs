using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class review_assignments
{
    public Guid id { get; set; }

    public string target_type { get; set; } = null!;

    public Guid target_id { get; set; }

    public Guid assignee_id { get; set; }

    public string assignee_role { get; set; } = null!;

    public string status { get; set; } = null!;

    public int priority { get; set; }

    public DateTime assigned_at { get; set; }

    /// <summary>Hạn hoàn thành duyệt do moderator chọn khi nhận duyệt (UTC).</summary>
    public DateTime? review_deadline_at { get; set; }

    public DateTime? completed_at { get; set; }

    public string? note { get; set; }

    public virtual users assignee { get; set; } = null!;
}
