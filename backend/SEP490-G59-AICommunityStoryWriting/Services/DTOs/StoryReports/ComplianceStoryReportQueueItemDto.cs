namespace Services.DTOs.StoryReports;

public class ComplianceStoryReportQueueItemDto
{
    public Guid StoryId { get; set; }
    public string StoryTitle { get; set; } = "";
    public string? StorySlug { get; set; }
    public int ReportCount { get; set; }
    /// <summary>Điểm severity gộp (max(dominant, weighted average) theo phiếu từng lý do).</summary>
    public double MaxSeverityScore { get; set; }
    public int TimeWeight { get; set; }
    public double PriorityScore { get; set; }
    public DateTime? OldestReportAtUtc { get; set; }
    public DateTime? NewestReportAtUtc { get; set; }
    public IReadOnlyList<string> DistinctReasonCodes { get; set; } = Array.Empty<string>();
    /// <summary>Từng người đã báo cáo + lý do (theo story_report_contributors).</summary>
    public IReadOnlyList<StoryReportContributorDto> Contributors { get; set; } = Array.Empty<StoryReportContributorDto>();
    public IReadOnlyList<string> StatusesPresent { get; set; } = Array.Empty<string>();

    /// <summary>Id các ticket NEW/IN_REVIEW trong nhóm truyện (đóng từng cái hoặc hàng loạt).</summary>
    public IReadOnlyList<Guid> OpenReportIds { get; set; } = Array.Empty<Guid>();

    public bool IsComplianceLocked { get; set; }
    public string? ComplianceClaimedByDisplayName { get; set; }
    /// <summary>Thời điểm compliance nhận lock (review_assignments.assigned_at, UTC).</summary>
    public DateTime? ComplianceClaimedAtUtc { get; set; }
    /// <summary>OK | NOTICE | WARNING | SEVERE | CRITICAL — theo số giờ từ lúc nhận lock.</summary>
    public string? ComplianceHandlingSlaStatus { get; set; }
    public string? ComplianceHandlingSlaMessageVi { get; set; }
    /// <summary>Số giờ kể từ lúc nhận lock (làm tròn 1 chữ số).</summary>
    public double? HoursSinceComplianceClaim { get; set; }
    public bool IsComplianceClaimedByMe { get; set; }

    public Guid? AuthorId { get; set; }
    public string? AuthorDisplayName { get; set; }

    public bool CommentsDisabled { get; set; }
    public bool ComplianceHidden { get; set; }
    public bool ComplianceFlagged { get; set; }
    public string? ComplianceFlagNote { get; set; }

    /// <summary>Đơn BAN/SUSPEND gửi admin từ luồng báo cáo truyện đang chờ — chỉ chặn đóng ticket, không khóa mọi thao tác.</summary>
    public bool HasPendingAdminActionRequest { get; set; }

    /// <summary>Đã gửi yêu cầu admin gỡ lock truyện — chặn mọi thao tác compliance trên truyện này.</summary>
    public bool HasPendingLockReleaseRequest { get; set; }
}
