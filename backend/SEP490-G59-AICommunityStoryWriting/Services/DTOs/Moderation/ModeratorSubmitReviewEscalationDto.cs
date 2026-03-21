namespace Services.DTOs.Moderation
{
    public class ModeratorSubmitReviewEscalationDto
    {
        /// <summary>STORY | CHAPTER</summary>
        public string TargetType { get; set; } = null!;

        public Guid TargetId { get; set; }

        /// <summary>EXTEND_DEADLINE | RELEASE_ASSIGNMENT</summary>
        public string RequestKind { get; set; } = null!;

        public string Reason { get; set; } = null!;

        /// <summary>Bắt buộc khi RequestKind = EXTEND_DEADLINE</summary>
        public DateTime? ProposedDeadlineAt { get; set; }
    }
}
