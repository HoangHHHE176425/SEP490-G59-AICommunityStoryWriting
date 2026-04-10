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
    public string? CommentUserAccountStatus { get; set; }
    public DateTime? CommentUserWritingSuspendedUntilUtc { get; set; }
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

    /// <summary>Đã gửi yêu cầu admin gỡ lock — chặn mọi thao tác compliance trên thread này.</summary>
    public bool HasPendingLockReleaseRequest { get; set; }

    /// <summary>Snapshot truyện: bình luận đang bị khóa (cấp truyện).</summary>
    public bool StoryCommentsDisabled { get; set; }

    /// <summary>Snapshot truyện: đang ẩn khỏi người dùng thường (compliance).</summary>
    public bool StoryComplianceHidden { get; set; }

    /// <summary>Tác giả truyện: đình chỉ quyền viết đến mốc này (UTC), nếu có.</summary>
    public DateTime? StoryAuthorWritingSuspendedUntilUtc { get; set; }

    /// <summary>Đơn chặn tài khoản đã được admin chấp nhận (thread này hoặc cùng truyện — luồng báo cáo truyện).</summary>
    public bool HasApprovedAdminBanRequest { get; set; }
}

public class ComplianceCommentReporterDetailDto
{
    public Guid EvidenceId { get; set; }
    public Guid ReportId { get; set; }
    public Guid ReporterUserId { get; set; }

    public string? ReporterDisplayName { get; set; }
    public DateTime? ReportedAtUtc { get; set; }
    public string? Description { get; set; }
    public string? ReasonLabelVi { get; set; }

    /// <summary>COMPLIANCE đã lưu đánh dấu xác minh cho request báo cáo này.</summary>
    public bool IsComplianceEvidenceVerified { get; set; }
}

