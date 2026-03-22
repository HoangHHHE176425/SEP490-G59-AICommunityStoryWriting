namespace Services.DTOs.StoryReports;

public class ComplianceLockRequestListItemDto
{
    public Guid Id { get; set; }
    public Guid StoryId { get; set; }
    public string? StoryTitle { get; set; }
    public Guid RequesterId { get; set; }
    public string? RequesterEmail { get; set; }
    public string? RequesterDisplayName { get; set; }
    public string? Message { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    /// <summary>Mức hiệu lực (lưu + tuổi đơn).</summary>
    public string UrgencyTier { get; set; } = "";
}
