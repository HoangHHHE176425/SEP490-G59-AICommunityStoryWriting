namespace Services.DTOs.StoryReports;

/// <summary>Compliance đánh dấu đã xử lý xong một (hoặc nhiều) báo cáo.</summary>
public class ComplianceResolveReportRequestDto
{
    /// <summary>RESOLVED | DISMISSED</summary>
    public string Status { get; set; } = "RESOLVED";
}
