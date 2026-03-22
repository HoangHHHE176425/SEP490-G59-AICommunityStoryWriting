namespace Services.DTOs.StoryReports;

public class ComplianceClaimStoryResultDto
{
    public int OpenReportCount { get; set; }

    /// <summary>Thời điểm nhận lock (UTC), đồng bộ <c>review_assignments.assigned_at</c>.</summary>
    public DateTime ClaimedAtUtc { get; set; }
}
