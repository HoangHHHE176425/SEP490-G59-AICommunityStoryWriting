using System.ComponentModel.DataAnnotations;
using Services.StoryReporting;

namespace Services.DTOs.CommentReports;

public class CreateCommentReportRequestDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;

    /// <summary>Bắt buộc có nội dung; tối thiểu 50 từ — kiểm tra chi tiết ở <see cref="UserReportDescriptionRules"/>.</summary>
    public string? Description { get; set; }
}

