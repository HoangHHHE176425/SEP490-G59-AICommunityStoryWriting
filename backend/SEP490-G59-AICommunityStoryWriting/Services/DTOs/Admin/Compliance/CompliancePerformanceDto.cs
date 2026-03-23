namespace Services.DTOs.Admin.Compliance;

public class CompliancePerformanceDto
{
    public Guid ComplianceUserId { get; set; }
    public string? ComplianceUserName { get; set; }
    public int ResolvedCount { get; set; }
    public int DismissedCount { get; set; }
    public int StoryReportResolvedCount { get; set; }
    public int CommentReportResolvedCount { get; set; }
    public int AdminActionRequestCount { get; set; }
    public int LockRequestCount { get; set; }
    public int Total => ResolvedCount + DismissedCount + AdminActionRequestCount + LockRequestCount;
}
