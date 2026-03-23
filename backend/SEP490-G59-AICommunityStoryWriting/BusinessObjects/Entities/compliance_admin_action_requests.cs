using System;

namespace BusinessObjects.Entities;

public partial class compliance_admin_action_requests
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    public Guid target_user_id { get; set; }

    /// <summary>BAN_USER | SUSPEND_AUTHOR_WRITING</summary>
    public string request_kind { get; set; } = null!;

    public string? message { get; set; }

    public DateTime? proposed_suspend_until_utc { get; set; }

    public string status { get; set; } = null!;

    public Guid requester_id { get; set; }

    public DateTime created_at { get; set; }

    public DateTime? resolved_at { get; set; }

    public Guid? resolved_by_id { get; set; }

    public string? resolution_note { get; set; }

    public string? resolution_action { get; set; }

    /// <summary>STANDARD | HIGH | CRITICAL</summary>
    public string urgency_tier { get; set; } = "STANDARD";

    public virtual stories? story { get; set; }

    public virtual users? target_user { get; set; }

    public virtual users? requester { get; set; }

    public virtual users? resolved_byNavigation { get; set; }
}
