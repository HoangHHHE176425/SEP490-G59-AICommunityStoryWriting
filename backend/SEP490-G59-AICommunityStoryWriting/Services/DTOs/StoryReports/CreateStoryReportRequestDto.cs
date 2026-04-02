using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.StoryReports;

public class CreateStoryReportRequestDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;

    /// <summary>Ma trận nghiệp vụ: tối đa 200 ký tự; DB/DAO có thể truncate khi gộp báo cáo.</summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}
