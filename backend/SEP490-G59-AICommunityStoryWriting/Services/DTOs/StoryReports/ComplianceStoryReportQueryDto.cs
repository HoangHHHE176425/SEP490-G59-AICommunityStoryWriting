namespace Services.DTOs.StoryReports;

public class ComplianceStoryReportQueryDto
{
    public Guid? StoryId { get; set; }

    /// <summary>Danh sách trạng thái, phân tách bởi dấu phẩy (vd: NEW,IN_REVIEW). Để xem mọi trạng thái dùng ALL.</summary>
    public string? Statuses { get; set; }

    public string? ReasonCode { get; set; }

    /// <summary>Tìm theo tiêu đề / slug truyện (contains).</summary>
    public string? Search { get; set; }

    public DateTime? CreatedFromUtc { get; set; }
    public DateTime? CreatedToUtc { get; set; }

    public int? MinPriority { get; set; }
    public int? MaxPriority { get; set; }

    /// <summary>priority_desc | priority_asc | oldest | newest</summary>
    public string? SortBy { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;

    /// <summary>true: gom theo truyện (hàng đợi); false: từng báo cáo.</summary>
    public bool GroupByStory { get; set; } = true;

    /// <summary>Giống moderator: all (mặc định) | unclaimed | mine — lọc theo lock <c>review_assignments</c> COMPLIANCE.</summary>
    public string? ClaimFilter { get; set; }

    /// <summary>Chỉ truyện đang được compliance gắn cờ theo dõi.</summary>
    public bool? FlaggedOnly { get; set; }
}
