namespace Services.DTOs.Chapters
{
    public class ChapterQueryDto
    {
        public Guid? StoryId { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public string? Status { get; set; }
        public string? AccessType { get; set; }
        public string? SortBy { get; set; } = "order_index"; // order_index, created_at, published_at, title
        public string? SortOrder { get; set; } = "asc"; // asc, desc
    }
}