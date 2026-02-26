namespace Services.DTOs.Notifications;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string? Type { get; set; }
    public string? Title { get; set; }
    public string? Content { get; set; }
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime? CreatedAt { get; set; }
}
