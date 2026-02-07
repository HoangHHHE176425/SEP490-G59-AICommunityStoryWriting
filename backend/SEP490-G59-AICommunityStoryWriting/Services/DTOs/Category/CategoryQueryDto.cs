namespace Services.DTOs.Categories
{
    public class CategoryQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string? Search { get; set; }
        public bool? IsActive { get; set; }
        public string? SortBy { get; set; } = "name"; // name, created_at, story_count
        public string? SortOrder { get; set; } = "asc"; // asc, desc
    }
}
