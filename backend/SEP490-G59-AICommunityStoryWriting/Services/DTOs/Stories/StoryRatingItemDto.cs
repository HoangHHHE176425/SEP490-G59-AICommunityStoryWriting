namespace Services.DTOs.Stories;

/// <summary>Một mục trong danh sách đánh giá của story (lịch sử đánh giá).</summary>
public class StoryRatingItemDto
{
    public Guid Id { get; set; }
    public Guid? UserId { get; set; }
    public string UserDisplayName { get; set; } = "";
    public int StarValue { get; set; }
    public string? ReviewText { get; set; }
    public DateTime? CreatedAt { get; set; }
}
