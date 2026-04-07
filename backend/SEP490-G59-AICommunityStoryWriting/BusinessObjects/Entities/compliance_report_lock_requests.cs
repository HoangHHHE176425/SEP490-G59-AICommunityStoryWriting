using System;

namespace BusinessObjects.Entities;

/// <summary>Yêu cầu gỡ lock / giao lại — compliance gửi, admin xử lý (STORY | COMMENT | …).</summary>
public partial class compliance_report_lock_requests
{
    public Guid id { get; set; }

    /// <summary>STORY | COMMENT | CHAPTER | APPEAL</summary>
    public string target_type { get; set; } = null!;

    public Guid target_id { get; set; }

    public Guid requester_id { get; set; }

    public string? message { get; set; }

    /// <summary>PENDING | APPROVED | REJECTED | CANCELLED</summary>
    public string status { get; set; } = "PENDING";

    public DateTime created_at { get; set; }

    public DateTime? resolved_at { get; set; }

    public Guid? resolved_by_id { get; set; }

    public string? resolution_note { get; set; }

    /// <summary>UNLOCK | REASSIGN | REJECT</summary>
    public string? resolution_action { get; set; }

    public virtual users? requester { get; set; }

    public virtual users? resolved_byNavigation { get; set; }
}
