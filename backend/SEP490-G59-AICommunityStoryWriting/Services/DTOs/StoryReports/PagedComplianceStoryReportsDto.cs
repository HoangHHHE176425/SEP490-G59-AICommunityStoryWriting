namespace Services.DTOs.StoryReports;

public class PagedComplianceStoryReportsDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<ComplianceStoryReportQueueItemDto> QueueItems { get; set; } = Array.Empty<ComplianceStoryReportQueueItemDto>();
    public IReadOnlyList<ComplianceStoryReportRowDto> Rows { get; set; } = Array.Empty<ComplianceStoryReportRowDto>();
}
