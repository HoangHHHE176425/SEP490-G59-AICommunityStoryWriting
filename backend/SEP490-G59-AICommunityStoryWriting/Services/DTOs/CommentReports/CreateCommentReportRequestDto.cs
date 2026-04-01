using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.CommentReports;

public class CreateCommentReportRequestDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;

    /// <summary>Ma trận nghiệp vụ: tối đa 200 ký tự.</summary>
    [MaxLength(200)]
    public string? Description { get; set; }
}

