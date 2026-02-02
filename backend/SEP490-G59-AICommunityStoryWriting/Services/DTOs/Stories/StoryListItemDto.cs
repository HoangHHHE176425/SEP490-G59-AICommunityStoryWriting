namespace Services.DTOs.Stories
{
    public class StoryListItemDto
    {
        public string Id { get; set; } = null!;
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string Status { get; set; } = null!;
        public string? CoverImage { get; set; }

        public int TotalChapters { get; set; }
        public long TotalViews { get; set; }
    }
}
