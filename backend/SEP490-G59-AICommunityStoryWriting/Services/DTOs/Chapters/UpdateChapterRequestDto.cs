namespace Services.DTOs.Chapters
{
    public class UpdateChapterRequestDto
    {
        public string Title { get; set; } = null!;
        public string? Content { get; set; }
        public int? OrderIndex { get; set; }
        public string? Status { get; set; }
        public string? AccessType { get; set; }
        public int? CoinPrice { get; set; }
        public decimal? AiContributionRatio { get; set; }
        public bool? IsAiClean { get; set; }
    }
}