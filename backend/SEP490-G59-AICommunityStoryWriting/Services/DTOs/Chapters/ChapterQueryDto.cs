namespace Services.DTOs.Chapters
{
    public class ChapterQueryDto
    {
        public Guid? StoryId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Status { get; set; }
        public string? AccessType { get; set; }
        public string? SortBy { get; set; } = "order_index"; // order_index, created_at, published_at
        public string? SortOrder { get; set; } = "asc"; // asc, desc
    }
}
