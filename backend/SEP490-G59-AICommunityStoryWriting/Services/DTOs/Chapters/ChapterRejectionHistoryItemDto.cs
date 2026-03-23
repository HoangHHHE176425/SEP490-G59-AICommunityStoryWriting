namespace Services.DTOs.Chapters;

/// <summary>Một lần moderator từ chối chương gốc (ghi trong moderation_logs, action REJECTED).</summary>
public class ChapterRejectionHistoryItemDto
{
    public string? Reason { get; set; }
    public DateTime? RejectedAt { get; set; }
    public Guid? ModeratorId { get; set; }
}
