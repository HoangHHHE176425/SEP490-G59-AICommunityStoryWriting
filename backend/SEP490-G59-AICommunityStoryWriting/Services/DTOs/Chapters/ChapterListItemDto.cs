namespace Services.DTOs.Chapters
{
    public class ChapterListItemDto
    {
        public Guid Id { get; set; }
        public Guid? StoryId { get; set; }
        /// <summary>Tiêu đề truyện (để hiển thị trên moderator dashboard).</summary>
        public string? StoryTitle { get; set; }
        public string Title { get; set; } = null!;
        public int OrderIndex { get; set; }
        public string? Status { get; set; }
        public string? AccessType { get; set; }
        public int? CoinPrice { get; set; }
        public int? WordCount { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}