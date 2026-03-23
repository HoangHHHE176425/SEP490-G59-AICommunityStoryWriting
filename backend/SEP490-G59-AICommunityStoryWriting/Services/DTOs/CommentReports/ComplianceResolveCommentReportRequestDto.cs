namespace Services.DTOs.CommentReports;

public class ComplianceResolveCommentReportRequestDto
{
    /// <summary>RESOLVED | DISMISSED</summary>
    public string Status { get; set; } = "RESOLVED";

    /// <summary>Chỉ áp dụng khi Status = RESOLVED.</summary>
    public bool HideComment { get; set; } = true;

    /// <summary>Chỉ áp dụng khi HideComment = true.</summary>
    public bool IncludeReplies { get; set; } = true;
}

