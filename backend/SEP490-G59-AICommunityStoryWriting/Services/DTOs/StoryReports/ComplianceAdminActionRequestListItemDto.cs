using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.StoryReports;

public class ComplianceAdminActionRequestListItemDto
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public Guid TargetUserId { get; set; }
    public string? TargetUserEmail { get; set; }
    public string? TargetUserDisplayName { get; set; }
    public string RequestKind { get; set; } = "";
    public string? Message { get; set; }
    public DateTime? ProposedSuspendUntilUtc { get; set; }
    public string Status { get; set; } = "";
    public Guid RequesterId { get; set; }
    public string? RequesterDisplayName { get; set; }
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>CRITICAL | STANDARD (HIGH gộp vào STANDARD) — từ urgency_tier + tuổi đơn.</summary>
    public string UrgencyTier { get; set; } = "";

    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolutionNote { get; set; }
    public string? ResolutionAction { get; set; }
}

public class CreateComplianceAdminActionRequestDto
{
    public string RequestKind { get; set; } = "";
    public string? Message { get; set; }
    /// <summary>Chỉ còn ý nghĩa với đơn SUSPEND cũ do admin duyệt (UTC). Compliance không gửi SUSPEND qua endpoint này nữa.</summary>
    public DateTime? ProposedSuspendUntilUtc { get; set; }
    /// <summary>Tùy chọn: mặc định lấy author của truyện.</summary>
    public Guid? TargetUserId { get; set; }
}

public class AdminResolveComplianceAdminActionRequestDto
{
    /// <summary>APPROVE | REJECT</summary>
    public string Decision { get; set; } = "";
    /// <summary>Mã lý do báo cáo (catalog truyện); bắt buộc khi resolve (UTC: null/blank → từ chối).</summary>
    public string? ReasonCode { get; set; }
    /// <summary>Mô tả bổ sung (ma trận report: tối đa 200 ký tự sau trim, cùng giới hạn UserReportDescriptionRules).</summary>
    public string? Description { get; set; }
    /// <summary>Ma trận: mô tả / ghi chú admin tối đa 2000 ký tự.</summary>
    [MaxLength(2000)]
    public string? AdminNote { get; set; }
    /// <summary>Admin có thể chỉnh ngày kết thúc tạm khóa viết (UTC); nếu null với SUSPEND thì dùng đề xuất của compliance.</summary>
    public DateTime? SuspendUntilUtc { get; set; }
}

public class ViolationLogListItemDto
{
    public Guid Id { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? PenaltyType { get; set; }
    public string? Reason { get; set; }
    public string? PolicyReference { get; set; }
    public string? ComplianceOfficerDisplayName { get; set; }
}
