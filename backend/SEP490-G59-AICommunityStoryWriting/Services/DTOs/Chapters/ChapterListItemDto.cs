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
        /// <summary>Phần trăm giống với bản AI (0–100) nếu đã lưu trên chương.</summary>
        public decimal? AiSimilarityPercent { get; set; }
        /// <summary>Tỷ lệ đóng góp AI (0–100) trên chương nếu có.</summary>
        public decimal? AiContributionRatio { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        /// <summary>Trong queue moderator: đã được moderator hiện tại nhận duyệt (lock).</summary>
        public bool IsClaimedByMe { get; set; }
        /// <summary>Trong queue moderator: tên người đang duyệt (nickname hoặc email).</summary>
        public string? ClaimedByDisplayName { get; set; }
        /// <summary>Trong queue moderator: thời điểm nhận duyệt.</summary>
        public DateTime? ClaimedAt { get; set; }

        /// <summary>Có đơn báo cáo lên admin đang chờ xử lý — moderator đang nhận duyệt không được duyệt/từ chối đến khi admin xử lý.</summary>
        public bool HasPendingEscalation { get; set; }

        /// <summary>Lý do từ chối (khi status = REJECTED).</summary>
        public string? RejectionReason { get; set; }
        /// <summary>Thời điểm moderator từ chối.</summary>
        public DateTime? RejectedAt { get; set; }

        /// <summary>Tất cả lần từ chối chương gốc (moderation_logs), cũ → mới — hiển thị cả sau khi tác giả gửi duyệt lại.</summary>
        public List<ChapterRejectionHistoryItemDto>? ModeratorRejectionHistory { get; set; }

        /// <summary>Tiêu đề version chờ duyệt (khi có); dùng cho sidebar moderator hiển thị ngay không cần gọi review-content.</summary>
        public string? PendingVersionTitle { get; set; }
        /// <summary>Số từ của version chờ duyệt (khi có).</summary>
        public int? PendingVersionWordCount { get; set; }

        /// <summary>Mốc tác giả gửi duyệt (submitted_for_review_at; fallback nếu cũ).</summary>
        public DateTime? PendingSince { get; set; }
        /// <summary>Hạn SLA duyệt (ưu tiên review_deadline_at khi đã claim; không claim thì mốc gửi + policy ngày). List chapter (GetAll/GetByStoryId) điền khi chương đang trong luồng duyệt.</summary>
        public DateTime? DeadlineAt { get; set; }
        /// <summary>Mức ưu tiên theo thời gian chờ từ mốc gửi (OnTime / Warning / Critical / Overdue).</summary>
        public string? TimeStatus { get; set; }
        /// <summary>Admin: thời điểm moderator duyệt/từ chối.</summary>
        public DateTime? ReviewedAt { get; set; }
        /// <summary>Admin: tên moderator đã duyệt/từ chối.</summary>
        public string? ReviewedByModeratorName { get; set; }

        /// <summary>Ghi chú admin khi từ chối đơn RELEASE_ASSIGNMENT (hủy nhận duyệt) do moderator hiện tại gửi (chapter hoặc truyện chứa chương). Phần gộp từ cấp truyện chỉ khi truyện đó còn chương chờ duyệt.</summary>
        public string? AdminRejectedReleaseNote { get; set; }
        /// <summary>Thời điểm admin từ chối đơn hủy nhận duyệt (theo bản ghi mới nhất áp dụng).</summary>
        public DateTime? AdminRejectedReleaseAt { get; set; }

        /// <summary>Ghi chú admin khi từ chối đơn EXTEND_DEADLINE (xin gia hạn).</summary>
        public string? AdminRejectedExtendNote { get; set; }
        /// <summary>Thời điểm admin từ chối đơn xin gia hạn.</summary>
        public DateTime? AdminRejectedExtendAt { get; set; }
    }
}