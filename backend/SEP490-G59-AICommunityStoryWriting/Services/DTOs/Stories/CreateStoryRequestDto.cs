using Microsoft.AspNetCore.Http;

namespace Services.DTOs.Stories
{
    public class CreateStoryRequestDto
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public int CategoryId { get; set; }

        // optional
        public int ExpectedChapters { get; set; } = 0;
        public int ReleaseFrequencyDays { get; set; } = 7;
        public string AgeRating { get; set; } = "ALL";

        public IFormFile? CoverImage { get; set; }
    }
}
