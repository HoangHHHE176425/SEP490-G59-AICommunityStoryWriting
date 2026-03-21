namespace Services.DTOs.Admin.Moderation
{
    /// <summary>Bộ lọc + phân trang thống kê hiệu suất moderator (admin).</summary>
    public class ModeratorPerformanceQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        /// <summary>Lọc log theo loại đích — STORY | CHAPTER; để trống = tất cả.</summary>
        public string? TargetType { get; set; }

        /// <summary>Tìm theo GUID moderator hoặc chuỗi trong tên hiển thị (nickname/email).</summary>
        public string? Search { get; set; }

        /// <summary>Chỉ hiện moderator có tổng (duyệt+từ chối) &gt;= giá trị.</summary>
        public int? MinTotalActions { get; set; }

        /// <summary>total | approved | rejected | reject_ratio | story_approved | chapter_approved | name</summary>
        public string? SortBy { get; set; }

        public string? SortOrder { get; set; }

        /// <summary>Lọc đúng một moderator (dropdown chung).</summary>
        public Guid? ModeratorId { get; set; }
    }
}
