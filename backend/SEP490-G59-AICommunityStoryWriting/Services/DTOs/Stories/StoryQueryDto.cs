namespace Services.DTOs.Stories
{
    public class StoryQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public int? CategoryId { get; set; }
        public int? AuthorId { get; set; }
        public string? Status { get; set; }
        public string? SortBy { get; set; } = "created_at"; // created_at, updated_at, total_views, avg_rating
        public string? SortOrder { get; set; } = "desc"; // asc, desc
    }
}
