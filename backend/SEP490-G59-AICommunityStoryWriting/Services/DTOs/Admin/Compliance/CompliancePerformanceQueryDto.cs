namespace Services.DTOs.Admin.Compliance;

public class CompliancePerformanceQueryDto
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public DateTime? DateFrom { get; set; }
    public DateTime? DateTo { get; set; }
    public Guid? ComplianceUserId { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; } // total | resolved | dismissed | admin_actions | lock_requests | name
    public string? SortOrder { get; set; } // asc | desc
}
