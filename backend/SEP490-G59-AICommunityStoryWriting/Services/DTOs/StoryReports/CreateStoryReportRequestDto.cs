using System.ComponentModel.DataAnnotations;
using Services.StoryReporting;

namespace Services.DTOs.StoryReports;

public class CreateStoryReportRequestDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;

    /// <summary>Bắt buộc có nội dung; tối thiểu 50 từ và tối đa ký tự — kiểm tra chi tiết ở <see cref="UserReportDescriptionRules"/>.</summary>
    [MaxLength(UserReportDescriptionRules.MaxLength)]
    public string? Description { get; set; }
}
