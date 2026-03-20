namespace Services.DTOs.Chapters
{
    public class ChapterResponseDto
    {
        public Guid Id { get; set; }
        public Guid? StoryId { get; set; }
        public string? StoryTitle { get; set; }
        public string Title { get; set; } = null!;
        public int OrderIndex { get; set; }
        public string? Content { get; set; }
        public string? Status { get; set; }
        public string? AccessType { get; set; }
        public int? CoinPrice { get; set; }
        /// <summary>Chỉ dùng cho màn reader: user hiện tại có đang được mở khóa chapter trả phí hay chưa.</summary>
        public bool IsUnlocked { get; set; }
        public int? WordCount { get; set; }
        public decimal? AiContributionRatio { get; set; }
        /// <summary>Phần trăm giống với bản AI (0–100), cập nhật khi chương PUBLISHED và gọi compare-chapter.</summary>
        public decimal? AiSimilarityPercent { get; set; }
        public bool IsAiClean { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }
    }
}