namespace Services.DTOs.StoryReports;

public class ComplianceOfficerAssignmentOptionDto
{
    public Guid UserId { get; set; }
    public string DisplayName { get; set; } = "";
    public string? Email { get; set; }
    /// <summary>Số truyện đang giữ lock báo cáo (COMPLIANCE_STORY_REPORTS).</summary>
    public int OpenStoryReportLocks { get; set; }
}
