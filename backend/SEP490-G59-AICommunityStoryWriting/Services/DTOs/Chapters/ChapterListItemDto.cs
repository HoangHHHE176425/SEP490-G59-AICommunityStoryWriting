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
        /// <summary>Phần trăm giống với bản AI (0–100), cập nhật khi chương PUBLISHED và gọi compare-chapter.</summary>
        public decimal? AiSimilarityPercent { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

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

        /// <summary>Tiêu đề version chờ duyệt (khi có); dùng cho sidebar moderator hiển thị ngay không cần gọi review-content.</summary>
        public string? PendingVersionTitle { get; set; }
        /// <summary>Số từ của version chờ duyệt (khi có).</summary>
        public int? PendingVersionWordCount { get; set; }

        /// <summary>Admin: thời điểm gửi duyệt.</summary>
        public DateTime? PendingSince { get; set; }
        /// <summary>Admin: hạn duyệt (PendingSince + 7 ngày).</summary>
        public DateTime? DeadlineAt { get; set; }
        /// <summary>Admin: trạng thái thời hạn — OnTime, Warning, Overdue.</summary>
        public string? TimeStatus { get; set; }
        /// <summary>Admin: thời điểm moderator duyệt/từ chối.</summary>
        public DateTime? ReviewedAt { get; set; }
        /// <summary>Admin: tên moderator đã duyệt/từ chối.</summary>
        public string? ReviewedByModeratorName { get; set; }
    }
}