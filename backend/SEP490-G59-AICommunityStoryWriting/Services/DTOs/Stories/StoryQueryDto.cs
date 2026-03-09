namespace Services.DTOs.Stories
{
    public class StoryQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public Guid? CategoryId { get; set; }
        /// <summary>Khi set: chỉ lấy truyện có ít nhất một category nằm trong list (dùng cho moderator theo category được gán).</summary>
        public List<Guid>? CategoryIds { get; set; }
        /// <summary>Khi set: loại trừ các story id (ví dụ: đã bị lock bởi moderator khác trong queue duyệt).</summary>
        public List<Guid>? ExcludeStoryIds { get; set; }
        /// <summary>Khi set: chỉ lấy truyện có id nằm trong list (ví dụ: lọc "đã nhận duyệt" trong queue moderator).</summary>
        public List<Guid>? IncludeStoryIds { get; set; }
        /// <summary>Khi set: thêm truyện có id trong list vào kết quả (bất kể Status). Dùng để hiển thị truyện có chương chờ duyệt dù truyện đã PUBLISHED.</summary>
        public List<Guid>? AlsoIncludeStoryIds { get; set; }
        public Guid? AuthorId { get; set; }
        public string? Status { get; set; }
        /// <summary>Khi set: chỉ lấy truyện có status nằm trong list (ví dụ: REJECTED, PENDING_REVIEW cho tab "Từ chối" vẫn hiển thị sau khi tác giả gửi lại).</summary>
        public List<string>? StatusIn { get; set; }
        public string? SortBy { get; set; } = "created_at"; // created_at, updated_at, total_views, avg_rating
        public string? SortOrder { get; set; } = "desc"; // asc, desc
    }
}