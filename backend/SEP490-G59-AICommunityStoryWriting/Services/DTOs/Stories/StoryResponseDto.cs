namespace Services.DTOs.Stories
{
    public class StoryResponseDto
    {
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }

        public int CategoryId { get; set; }
        public string AuthorId { get; set; } = null!;

        public string? CoverImage { get; set; }
        public string Status { get; set; } = null!;
        public string AgeRating { get; set; } = null!;

        public DateTime? CreatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
    }
}
