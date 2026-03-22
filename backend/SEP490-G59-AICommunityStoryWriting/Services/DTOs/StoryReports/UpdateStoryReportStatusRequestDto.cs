using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.StoryReports;

public class UpdateStoryReportStatusRequestDto
{
    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = null!;
}
