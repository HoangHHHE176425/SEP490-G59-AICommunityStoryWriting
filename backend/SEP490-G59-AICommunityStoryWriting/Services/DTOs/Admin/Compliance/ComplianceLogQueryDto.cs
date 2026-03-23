namespace Services.DTOs.Admin.Compliance;

public class ComplianceLogQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public Guid? ComplianceUserId { get; set; }
    public string? Source { get; set; } // REPORT_RESOLUTION | ADMIN_ACTION_REQUEST | LOCK_REQUEST
    public string? Status { get; set; }
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } // created_at | source | status
    public string? SortOrder { get; set; } // asc | desc
}
