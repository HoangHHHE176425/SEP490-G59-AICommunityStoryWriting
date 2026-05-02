namespace Services.DTOs.StoryReports;

public class CreateStoryReportResultDto
{
    public Guid ReportId { get; set; }
    public string Message { get; set; } = string.Empty;
}
