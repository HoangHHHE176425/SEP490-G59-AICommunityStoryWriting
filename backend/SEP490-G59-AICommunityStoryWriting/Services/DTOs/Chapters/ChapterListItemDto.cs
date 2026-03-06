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

        /// <summary>Trong queue moderator: đã được moderator hiện tại nhận duyệt (lock).</summary>
        public bool IsClaimedByMe { get; set; }
        /// <summary>Trong queue moderator: tên người đang duyệt (nickname hoặc email).</summary>
        public string? ClaimedByDisplayName { get; set; }
        /// <summary>Trong queue moderator: thời điểm nhận duyệt.</summary>
        public DateTime? ClaimedAt { get; set; }
        /// <summary>Lý do từ chối (khi status = REJECTED).</summary>
        public string? RejectionReason { get; set; }
        /// <summary>Thời điểm moderator từ chối.</summary>
        public DateTime? RejectedAt { get; set; }
    }
}