using System;

namespace Services.DTOs.CommentReports;

public class PagedComplianceCommentReportsDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public System.Collections.Generic.IReadOnlyList<ComplianceCommentReportRowDto> Rows { get; set; }
        = Array.Empty<ComplianceCommentReportRowDto>();
}

