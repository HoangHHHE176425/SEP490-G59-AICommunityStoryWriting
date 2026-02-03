namespace Services.DTOs.Stories
{
    public class StoryResponseDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }

        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int? AuthorId { get; set; }
        public string? AuthorName { get; set; }

        public string? CoverImage { get; set; }
        public string? Status { get; set; }
        public string? SaleType { get; set; }
        public string? AccessType { get; set; }
        public string? AgeRating { get; set; }

        public int? ExpectedChapters { get; set; }
        public int? ReleaseFrequencyDays { get; set; }
        public int? TotalChapters { get; set; }
        public long? TotalViews { get; set; }
        public int? TotalFavorites { get; set; }
        public decimal? AvgRating { get; set; }
        public int? WordCount { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? LastPublishedAt { get; set; }
    }
}
