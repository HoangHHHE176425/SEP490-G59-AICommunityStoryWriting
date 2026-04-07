namespace Services.DTOs.StoryReports;

public class ComplianceLockRequestListItemDto
{
    public Guid Id { get; set; }

    /// <summary>STORY | COMMENT | …</summary>
    public string TargetType { get; set; } = "STORY";

    /// <summary>story_id hoặc comment_id tùy <see cref="TargetType"/>.</summary>
    public Guid TargetId { get; set; }

    /// <summary>Truyện liên quan (với COMMENT = truyện chứa comment).</summary>
    public Guid StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public Guid RequesterId { get; set; }
    public string? RequesterEmail { get; set; }
    public string? RequesterDisplayName { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>CRITICAL | STANDARD (HIGH gộp vào STANDARD) — từ urgency_tier + tuổi đơn.</summary>
    public string UrgencyTier { get; set; } = "";

    public DateTime? ResolvedAtUtc { get; set; }

    /// <summary>Ghi chú / lý do khi admin xử lý xong (đặc biệt khi từ chối).</summary>
    public string? ResolutionNote { get; set; }

    /// <summary>UNLOCK | REASSIGN | REJECT.</summary>
    public string? ResolutionAction { get; set; }
}
