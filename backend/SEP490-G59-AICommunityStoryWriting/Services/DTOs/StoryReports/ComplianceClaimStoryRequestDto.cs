namespace Services.DTOs.StoryReports;

/// <summary>Hạn xử lý (UTC) khi nhận lock; mặc định +7 ngày nếu null.</summary>
public class ComplianceClaimStoryRequestDto
{
    public DateTime? ReviewDeadlineAtUtc { get; set; }
}
