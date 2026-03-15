namespace Services.DTOs.Admin.Moderation
{
    public class ModerationLogEntryDto
    {
        public long Id { get; set; }
        public string TargetType { get; set; } = null!;
        public Guid? TargetId { get; set; }
        public string? TargetTitle { get; set; }
        public string? Action { get; set; }
        public Guid? ModeratorId { get; set; }
        public string? ModeratorName { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? RejectionReason { get; set; }
        public int? ProcessingTimeMs { get; set; }
    }
}
