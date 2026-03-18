namespace Services.DTOs.Moderation
{
    /// <summary>Lịch sử phiên bản chương bị từ chối (dùng cho màn quản lý xuất bản).</summary>
    public class RejectedChapterVersionItemDto
    {
        public Guid Id { get; set; } // versionId
        public Guid? ChapterId { get; set; }
        public Guid? StoryId { get; set; }
        public string? StoryTitle { get; set; }
        public string? ChapterTitle { get; set; }
        public int? ChapterOrderIndex { get; set; }
        public int VersionNumber { get; set; }
        public string? TitleSnapshot { get; set; }
        public string? Status { get; set; }
        public int? WordCount { get; set; }
        public string? RejectionReason { get; set; }
        public DateTime? RejectedAt { get; set; }
    }
}

