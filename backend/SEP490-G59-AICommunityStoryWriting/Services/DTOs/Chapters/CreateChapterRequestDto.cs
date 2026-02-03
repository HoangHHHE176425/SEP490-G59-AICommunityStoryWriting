namespace Services.DTOs.Chapters
{
    public class CreateChapterRequestDto
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Content { get; set; }
        public int OrderIndex { get; set; }
        public string? AccessType { get; set; } = "FREE";
        public int? CoinPrice { get; set; } = 0;
        public decimal? AiContributionRatio { get; set; } = 0;
        public bool IsAiClean { get; set; } = false;
    }
}
