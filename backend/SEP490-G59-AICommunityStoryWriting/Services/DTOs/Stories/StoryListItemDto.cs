namespace Services.DTOs.Stories
{
    public class StoryListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }
        public string? Status { get; set; }
        public string? CoverImage { get; set; }

        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int? AuthorId { get; set; }
        public string? AuthorName { get; set; }

        public int? TotalChapters { get; set; }
        public long? TotalViews { get; set; }
        public int? TotalFavorites { get; set; }
        public decimal? AvgRating { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
