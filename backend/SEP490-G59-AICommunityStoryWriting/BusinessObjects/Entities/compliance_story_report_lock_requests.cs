using System;

namespace BusinessObjects.Entities;

/// <summary>Yêu cầu gỡ lock / giao lại báo cáo truyện — compliance gửi, admin xử lý.</summary>
public partial class compliance_story_report_lock_requests
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    public Guid requester_id { get; set; }

    public string? message { get; set; }

    /// <summary>PENDING | APPROVED | REJECTED</summary>
    public string status { get; set; } = "PENDING";

    public DateTime created_at { get; set; }

    public DateTime? resolved_at { get; set; }

    public Guid? resolved_by_id { get; set; }

    public string? resolution_note { get; set; }

    /// <summary>UNLOCK | REASSIGN | REJECT (ghi nhận khi admin duyệt).</summary>
    public string? resolution_action { get; set; }

    /// <summary>STANDARD | HIGH | CRITICAL — compliance chọn khi gửi; merge với tuổi đơn.</summary>
    public string urgency_tier { get; set; } = "STANDARD";

    public virtual stories? story { get; set; }

    public virtual users? requester { get; set; }

    public virtual users? resolved_byNavigation { get; set; }
}
