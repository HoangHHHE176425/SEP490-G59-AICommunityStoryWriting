using System;
using System.Collections.Generic;

namespace Services.DTOs.CommentReports;

public class ComplianceCommentReportRowDto
{
    public Guid ReportId { get; set; }
    public Guid CommentId { get; set; }
    public Guid StoryId { get; set; }
    /// <summary>null = bình luận cấp truyện; có giá trị = bình luận trên chương đó.</summary>
    public Guid? ChapterId { get; set; }
    public string? StoryTitle { get; set; }

    public Guid CommentUserId { get; set; }
    public string? CommentUserDisplayName { get; set; }
    public string? CommentUserEmail { get; set; }
    public string? CommentContent { get; set; }
    public bool IsCommentThreadHidden { get; set; }

    public string? ReasonCode { get; set; }
    public double SeverityScore { get; set; }
    // Queue priority (giống report story)
    public double PriorityScore { get; set; }
    public int ReportCount { get; set; }
    public int TimeWeight { get; set; }
    public string? ReasonLabelVi { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }

    public Guid ReporterId { get; set; }
    public string? ReporterEmail { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    // Lock/claim (giống compliance story reports)
    public bool IsComplianceLocked { get; set; }
    public bool IsComplianceClaimedByMe { get; set; }
    public string? ComplianceClaimedByDisplayName { get; set; }
    public DateTime? ComplianceClaimedAtUtc { get; set; }
    public string? ComplianceHandlingSlaStatus { get; set; }
    public string? ComplianceHandlingSlaMessageVi { get; set; }
    public double? HoursSinceComplianceClaim { get; set; }

    // Cảnh báo khi thread có reply của ADMIN/MODERATOR.
    public bool HasAdminOrModeratorReplyInThread { get; set; }
    public string? AdminOrModeratorReplyWarningVi { get; set; }

    // Story-like: liệt kê tất cả người report + tất cả lý do report trong thread.
    public IReadOnlyList<string> ReporterDisplayNames { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ComplianceCommentReporterDetailDto> ReporterDetails { get; set; } = Array.Empty<ComplianceCommentReporterDetailDto>();
    public IReadOnlyList<string> ReasonSummaryVi { get; set; } = Array.Empty<string>();

    // Khi COMPLIANCE đã gửi đơn lên ADMIN (requestKind BAN_USER/SUSPEND_AUTHOR_WRITING) đang PENDING,
    // thì không cho phép COMPLIANCE thao tác tiếp trên các ticket comment report liên quan.
    public bool HasPendingAdminActionRequest { get; set; }
}

public class ComplianceCommentReporterDetailDto
{
    public string? ReporterDisplayName { get; set; }
    public DateTime? ReportedAtUtc { get; set; }
    public string? Description { get; set; }
    public string? ReasonLabelVi { get; set; }
}

