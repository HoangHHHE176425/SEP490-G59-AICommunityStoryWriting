namespace Services.DTOs.Admin.Moderation
{
    /// <summary>Bộ lọc + phân trang cho moderation logs (admin theo dõi hoạt động moderator).</summary>
    public class ModerationLogQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        /// <summary>Tìm theo id log (số), GUID moderator/target, lý do từ chối, tiêu đề story/chapter.</summary>
        public string? Search { get; set; }

        public Guid? ModeratorId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        public string? Action { get; set; }
        public string? TargetType { get; set; }

        /// <summary>Lọc đúng một nội dung (story/chapter id).</summary>
        public Guid? TargetId { get; set; }

        public int? ProcessingTimeMinMs { get; set; }
        public int? ProcessingTimeMaxMs { get; set; }

        /// <summary>created_at | processing_time_ms | id</summary>
        public string? SortBy { get; set; }

        public string? SortOrder { get; set; }
    }
}
