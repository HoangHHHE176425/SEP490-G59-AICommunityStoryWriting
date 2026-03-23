using System;

namespace Services.DTOs.CommentReports;

public class ComplianceClaimCommentResultDto
{
    public int OpenReportCount { get; set; }
    public DateTime ClaimedAtUtc { get; set; }
}

