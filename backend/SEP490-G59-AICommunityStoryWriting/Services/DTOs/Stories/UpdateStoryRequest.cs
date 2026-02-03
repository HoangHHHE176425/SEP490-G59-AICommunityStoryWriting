using Microsoft.AspNetCore.Http;

namespace Services.DTOs.Stories
{
    public class UpdateStoryRequestDto
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
        public string? CoverImageUrl { get; set; }
    }

    public class UpdateStoryWithImageRequestDto
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
