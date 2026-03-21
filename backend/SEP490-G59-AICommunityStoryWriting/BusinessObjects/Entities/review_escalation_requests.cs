using System;

namespace BusinessObjects.Entities;

/// <summary>
/// Escalation request for moderation workflow.
/// Any role (user, author, moderator) can submit a request,
/// and a higher role (moderator/admin) resolves it.
/// </summary>
public partial class review_escalation_requests
{
    public Guid id { get; set; }

    /// <summary>
    /// Type of target: story | chapter | comment | user
    /// </summary>
    public string target_type { get; set; } = null!;

    public Guid target_id { get; set; }

    /// <summary>
    /// User who created the escalation request
    /// </summary>
    public Guid sender_id { get; set; }

    /// <summary>
    /// User who resolved the request (moderator/admin)
    /// </summary>
    public Guid? resolver_id { get; set; }

    /// <summary>
    /// Request type:
    /// EXTEND_DEADLINE | RELEASE_ASSIGNMENT | REPORT_CONTENT | APPEAL
    /// </summary>
    public string request_kind { get; set; } = null!;

    public string reason { get; set; } = null!;

    /// <summary>
    /// Proposed deadline from requester
    /// </summary>
    public DateTime? proposed_deadline_at { get; set; }

    /// <summary>
    /// PENDING | APPROVED | REJECTED | RESOLVED
    /// </summary>
    public string status { get; set; } = null!;

    /// <summary>
    /// Note from resolver
    /// </summary>
    public string? resolver_note { get; set; }

    /// <summary>
    /// Confirmed deadline by resolver
    /// </summary>
    public DateTime? confirmed_deadline_at { get; set; }

    public DateTime created_at { get; set; }

    public DateTime? resolved_at { get; set; }
}