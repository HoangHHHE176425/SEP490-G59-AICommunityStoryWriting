namespace Services.DTOs.StoryReports;

public class ComplianceStoryReportRowDto
{
    public Guid ReportId { get; set; }
    public Guid StoryId { get; set; }
    public string StoryTitle { get; set; } = "";
    public Guid? ReporterId { get; set; }
    public string? ReporterEmail { get; set; }
    public string? ReasonCode { get; set; }
    public int SeverityScore { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public Guid? AssignedTo { get; set; }
    public string? AssignedToEmail { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Người compliance đã đóng ticket (nếu có).</summary>
    public Guid? ComplianceResolvedBy { get; set; }

    public bool IsComplianceLocked { get; set; }
    public string? ComplianceClaimedByDisplayName { get; set; }
    public DateTime? ComplianceClaimedAtUtc { get; set; }
    public string? ComplianceHandlingSlaStatus { get; set; }
    public string? ComplianceHandlingSlaMessageVi { get; set; }
    public double? HoursSinceComplianceClaim { get; set; }
    public bool IsComplianceClaimedByMe { get; set; }

    /// <summary>Cùng truyện: danh sách mọi người đã báo (không chỉ reporter trên dòng reports).</summary>
    public IReadOnlyList<StoryReportContributorDto> Contributors { get; set; } = Array.Empty<StoryReportContributorDto>();

    public Guid? AuthorId { get; set; }
    public string? AuthorAccountStatus { get; set; }
    public DateTime? AuthorWritingSuspendedUntilUtc { get; set; }
    public bool CommentsDisabled { get; set; }
    public bool ComplianceHidden { get; set; }
    public bool ComplianceFlagged { get; set; }
    public string? ComplianceFlagNote { get; set; }

    public bool HasPendingAdminActionRequest { get; set; }
    public bool HasPendingLockReleaseRequest { get; set; }
}
