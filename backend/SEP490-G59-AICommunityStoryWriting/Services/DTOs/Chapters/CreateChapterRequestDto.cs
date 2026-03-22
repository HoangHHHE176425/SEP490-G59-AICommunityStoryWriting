namespace Services.DTOs.Chapters
{
    public class CreateChapterRequestDto
    {
        public Guid StoryId { get; set; }
        public string Title { get; set; } = null!;
        public string? Content { get; set; }
        public int OrderIndex { get; set; }
        public string? Status { get; set; } = "DRAFT";
        public string? AccessType { get; set; } = "FREE";
        public int? CoinPrice { get; set; } = 0;
        public decimal? AiContributionRatio { get; set; } = 0;
        public bool IsAiClean { get; set; } = false;

        /// <summary>Nếu tạo chương từ bản AI (co-create): truyền id bản ghi ai_generated_content. Nội dung sẽ lấy từ ai_output (nếu Content trống); sau khi tạo sẽ gán chapter_id và chapter_index = order_index. So sánh: POST compare-chapter với chapterId.</summary>
        public Guid? AiGeneratedContentId { get; set; }
    }
}