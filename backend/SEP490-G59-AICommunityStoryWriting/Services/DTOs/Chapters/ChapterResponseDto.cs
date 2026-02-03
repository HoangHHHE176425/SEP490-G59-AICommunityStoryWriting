namespace Services.DTOs.Chapters
{
    public class ChapterResponseDto
    {
        public int Id { get; set; }
        public int StoryId { get; set; }
        public string? StoryTitle { get; set; }
        public string Title { get; set; } = null!;
        public int OrderIndex { get; set; }
        public string? Content { get; set; }
        public string? Status { get; set; }
        public string? AccessType { get; set; }
        public int? CoinPrice { get; set; }
        public int? WordCount { get; set; }
        public decimal? AiContributionRatio { get; set; }
        public bool IsAiClean { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
