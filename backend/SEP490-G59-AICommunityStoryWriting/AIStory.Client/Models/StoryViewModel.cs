namespace AIStory.Client.Models
{
    public class StoryViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }
        public int? CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public int? AuthorId { get; set; }
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
    }

    public class StoryListItemViewModel
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
        public int? TotalChapters { get; set; }
        public long? TotalViews { get; set; }
        public int? TotalFavorites { get; set; }
        public decimal? AvgRating { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class PagedResultViewModel<T>
    {
        public IEnumerable<T> Items { get; set; } = new List<T>();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
        public bool HasPreviousPage => Page > 1;
        public bool HasNextPage => Page < TotalPages;
    }

    public class CreateStoryViewModel
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public int CategoryId { get; set; }
        public int ExpectedChapters { get; set; } = 0;
        public int ReleaseFrequencyDays { get; set; } = 7;
        public string AgeRating { get; set; } = "ALL";
        public IFormFile? CoverImage { get; set; }
    }

    public class UpdateStoryViewModel
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public int CategoryId { get; set; }
        public string? Status { get; set; }
        public string? SaleType { get; set; }
        public string? AccessType { get; set; }
        public string? AgeRating { get; set; }
        public int? ExpectedChapters { get; set; }
        public int? ReleaseFrequencyDays { get; set; }
        public IFormFile? CoverImage { get; set; }
    }
}
