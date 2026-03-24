using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.StoryReports;

public class CreateStoryReportRequestDto
{
    [Required]
    [MaxLength(50)]
    public string ReasonCode { get; set; } = null!;

    [MaxLength(4000)]
    public string? Description { get; set; }
}
