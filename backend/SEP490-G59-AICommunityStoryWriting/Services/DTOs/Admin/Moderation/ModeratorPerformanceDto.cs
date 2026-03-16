namespace Services.DTOs.Admin.Moderation
{
    public class ModeratorPerformanceDto
    {
        public Guid ModeratorId { get; set; }
        public string? ModeratorName { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int Total => ApprovedCount + RejectedCount;
    }
}
