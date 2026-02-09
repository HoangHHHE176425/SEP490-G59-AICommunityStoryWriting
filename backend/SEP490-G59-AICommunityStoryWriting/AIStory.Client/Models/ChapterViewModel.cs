namespace AIStory.Client.Models
{
    public class ChapterViewModel
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
        public int? WordCount { get; set; }
        public decimal? AiContributionRatio { get; set; }
        public bool IsAiClean { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class ChapterListItemViewModel
    {
        public Guid Id { get; set; }
        public Guid? StoryId { get; set; }
        public string Title { get; set; } = null!;
        public int OrderIndex { get; set; }
        public string? Status { get; set; }
        public string? AccessType { get; set; }
        public int? CoinPrice { get; set; }
        public int? WordCount { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
    }

    public class CreateChapterViewModel
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

    public class UpdateChapterViewModel
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