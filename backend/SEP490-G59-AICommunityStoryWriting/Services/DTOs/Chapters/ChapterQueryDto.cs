namespace Services.DTOs.Chapters
{
    public class ChapterQueryDto
    {
        public Guid? StoryId { get; set; }
        /// <summary>Khi set: chỉ lấy chapter thuộc truyện có id nằm trong list (dùng cho moderator theo category được gán).</summary>
        public List<Guid>? StoryIds { get; set; }
        /// <summary>Khi set: loại trừ các chapter id (ví dụ: đã bị lock bởi moderator khác trong queue duyệt).</summary>
        public List<Guid>? ExcludeChapterIds { get; set; }
        /// <summary>Khi set: chỉ lấy chapter có id nằm trong list (ví dụ: lọc "đã nhận duyệt").</summary>
        public List<Guid>? IncludeChapterIds { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public string? Status { get; set; }
        /// <summary>Khi set: chỉ lấy chapter có status nằm trong list (ví dụ: APPROVED, REJECTED cho "đã duyệt").</summary>
        public List<string>? StatusIn { get; set; }
        /// <summary>Khi set: lấy chapter có status PENDING_REVIEW HOẶC (status PUBLISHED và id nằm trong list) — dùng cho queue moderator gồm chapter chờ duyệt lần đầu và chapter đã xuất bản có version gửi chỉnh sửa.</summary>
        public List<Guid>? PendingVersionChapterIds { get; set; }
        public string? AccessType { get; set; }
        public string? SortBy { get; set; } = "order_index"; // order_index, created_at, published_at, title
        public string? SortOrder { get; set; } = "asc"; // asc, desc
    }
}
