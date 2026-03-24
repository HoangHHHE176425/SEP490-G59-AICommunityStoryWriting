namespace Services.DTOs.StoryReports;

/// <summary>Một lần báo cáo từ một user (bảng story_report_contributors).</summary>
public class StoryReportContributorDto
{
    public Guid UserId { get; set; }
    public string? UserEmail { get; set; }
    public string ReasonCode { get; set; } = "";
    public string? ReasonLabelVi { get; set; }
    public string? Description { get; set; }
    public DateTime ReportedAtUtc { get; set; }

    /// <summary>Khi chưa có dòng contributor trong DB nhưng ticket gộp nhiều người.</summary>
    public string? DetailNote { get; set; }
}
