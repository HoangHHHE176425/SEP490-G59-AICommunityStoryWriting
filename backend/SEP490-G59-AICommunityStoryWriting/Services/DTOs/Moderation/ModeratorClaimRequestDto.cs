namespace Services.DTOs.Moderation
{
    /// <summary>Body khi moderator nhận duyệt: hạn hoàn thành kiểm duyệt (UTC, ISO 8601).</summary>
    public class ModeratorClaimRequestDto
    {
        public DateTime ReviewDeadlineAt { get; set; }
    }
}
