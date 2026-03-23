namespace Services.DTOs.Chapters
{
    public class ChapterVersionListItemDto
    {
        public Guid Id { get; set; }
        public Guid ChapterId { get; set; }
        public int VersionNumber { get; set; }
        public string? TitleSnapshot { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? ReviewedAt { get; set; }

        /// <summary>% giống AI (0–100) sau khi gọi API so sánh version snapshot.</summary>
        public decimal? AiSimilarityPercent { get; set; }
    }
}
