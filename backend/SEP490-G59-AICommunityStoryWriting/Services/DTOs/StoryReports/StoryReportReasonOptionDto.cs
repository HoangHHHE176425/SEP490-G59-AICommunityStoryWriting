namespace Services.DTOs.StoryReports;

public class StoryReportReasonOptionDto
{
    public string Code { get; set; } = null!;
    public string Label { get; set; } = null!;
    public string LabelVi { get; set; } = null!;
    public string SeverityLevel { get; set; } = null!;
    public int SeverityScore { get; set; }
}
