namespace Services.DTOs.Admin;

/// <summary>Bộ lọc log đơn gửi admin (moderator + compliance).</summary>
public class UnifiedEscalationLogQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>Để trống = tất cả. REVIEW_ESCALATION | COMPLIANCE_LOCK | COMPLIANCE_ADMIN_ACTION</summary>
    public string? Source { get; set; }

    public string? Search { get; set; }

    /// <summary>PENDING | APPROVED | REJECTED — để trống = tất cả.</summary>
    public string? Status { get; set; }

    /// <summary>Moderator: EXTEND_DEADLINE | RELEASE_ASSIGNMENT. Compliance hành động: BAN_USER | SUSPEND_AUTHOR_WRITING. Để trống = tất cả.</summary>
    public string? RequestKind { get; set; }

    /// <summary>Chỉ đơn moderator: STORY | CHAPTER</summary>
    public string? TargetType { get; set; }

    public Guid? SenderId { get; set; }
    public Guid? ResolverId { get; set; }

    public DateTime? CreatedFrom { get; set; }
    public DateTime? CreatedTo { get; set; }
    public DateTime? ResolvedFrom { get; set; }
    public DateTime? ResolvedTo { get; set; }

    /// <summary>created_at | resolved_at</summary>
    public string? SortBy { get; set; }

    /// <summary>asc | desc</summary>
    public string? SortOrder { get; set; }
}

/// <summary>Một dòng trong log thống nhất.</summary>
public class UnifiedEscalationLogItemDto
{
    public string Source { get; set; } = null!;
    public Guid Id { get; set; }
    public string? Status { get; set; }
    public string UrgencyTier { get; set; } = null!;
    public string? KindLabel { get; set; }
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? TargetTitle { get; set; }
    public string? SummaryText { get; set; }
    public Guid SenderId { get; set; }
    public string? SenderName { get; set; }
    public Guid? ResolverId { get; set; }
    public string? ResolverName { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public string? ResolverNote { get; set; }
}
