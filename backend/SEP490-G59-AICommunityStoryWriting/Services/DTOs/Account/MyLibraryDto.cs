namespace Services.DTOs.Account
{
    /// <summary>Thông tin truyện trong danh sách "đang theo dõi".</summary>
    public class FollowedStoryItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string? Slug { get; set; }
        public string? CoverImage { get; set; }
        public string? Summary { get; set; }
        public Guid? AuthorId { get; set; }
        public string? AuthorName { get; set; }
        public string? Status { get; set; }
        public int? PublishedChaptersCount { get; set; }
        public DateTime? LatestUpdatedAt { get; set; }
    }

    /// <summary>Thông tin tác giả trong danh sách "đang theo dõi".</summary>
    public class FollowedAuthorItemDto
    {
        public Guid AuthorId { get; set; }
        public string AuthorName { get; set; } = null!;
    }

    /// <summary>Một mục lịch sử đọc: truyện + chương đang đọc dở.</summary>
    public class ReadingHistoryItemDto
    {
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = null!;
        public string? CoverImage { get; set; }
        public Guid LastReadChapterId { get; set; }
        public string? LastReadChapterTitle { get; set; }
        public int? LastReadChapterOrder { get; set; }
        public DateTime LastReadAt { get; set; }
    }

    public class MyLibraryResponseDto
    {
        public List<FollowedStoryItemDto> FollowedStories { get; set; } = new();
        public List<FollowedAuthorItemDto> FollowedAuthors { get; set; } = new();
        public List<ReadingHistoryItemDto> ReadingHistory { get; set; } = new();
    }
}
