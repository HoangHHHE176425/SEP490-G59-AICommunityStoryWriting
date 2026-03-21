namespace Services.DTOs.Admin.Moderation
{
    public class ModeratorPerformanceDto
    {
        public Guid ModeratorId { get; set; }
        public string? ModeratorName { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }

        public int StoryApprovedCount { get; set; }
        public int StoryRejectedCount { get; set; }
        public int ChapterApprovedCount { get; set; }
        public int ChapterRejectedCount { get; set; }

        public int Total => ApprovedCount + RejectedCount;

        /// <summary>Tỷ lệ từ chối trong khoảng lọc (0–1), null nếu không có hành động.</summary>
        public double? RejectRatio => Total > 0 ? Math.Round((double)RejectedCount / Total, 4) : null;
    }
}
