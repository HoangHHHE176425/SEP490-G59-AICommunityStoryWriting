namespace Services.DTOs.Stories
{
    public class StoryListItemDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }
        public string? Status { get; set; }
        /// <summary>ONGOING = Đang ra, COMPLETED = Hoàn thành, HIATUS = Tạm dừng</summary>
        public string? StoryProgressStatus { get; set; }
        public string? CoverImage { get; set; }

        public List<Guid> CategoryIds { get; set; } = new();
        public string? CategoryNames { get; set; }
        public Guid? AuthorId { get; set; }
        public string? AuthorName { get; set; }
        /// <summary>Độ tuổi phù hợp: ALL, 13+, 16+, 18+</summary>
        public string? AgeRating { get; set; }

        public int? TotalChapters { get; set; }
        /// <summary>Số chapter đã PUBLISHED.</summary>
        public int? PublishedChaptersCount { get; set; }
        public long? TotalViews { get; set; }
        public int? TotalComments { get; set; }
        public int? TotalFavorites { get; set; }
        public decimal? AvgRating { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>Thời gian cập nhật gần nhất (max giữa story.updated_at và chapter.updated_at mới nhất).</summary>
        public DateTime? LatestUpdatedAt { get; set; }

        /// <summary>Trong queue moderator: đã được moderator hiện tại nhận duyệt (lock).</summary>
        public bool IsClaimedByMe { get; set; }
        /// <summary>Trong queue moderator: tên người đang duyệt (nickname hoặc email).</summary>
        public string? ClaimedByDisplayName { get; set; }
        /// <summary>Trong queue moderator: thời điểm nhận duyệt.</summary>
        public DateTime? ClaimedAt { get; set; }

        /// <summary>Có đơn báo cáo lên admin đang chờ xử lý — moderator đang nhận duyệt không được duyệt/từ chối đến khi admin xử lý.</summary>
        public bool HasPendingEscalation { get; set; }

        /// <summary>Lý do từ chối (lịch sử từ chối; có thể có ngay cả khi story hiện đã PUBLISHED).</summary>
        public string? RejectionReason { get; set; }
        /// <summary>Thời điểm moderator từ chối (lịch sử từ chối).</summary>
        public DateTime? RejectedAt { get; set; }

        /// <summary>Mốc tác giả gửi duyệt (submitted_for_review_at; fallback ước lượng nếu dữ liệu cũ).</summary>
        public DateTime? PendingSince { get; set; }
        /// <summary>Moderator queue: không dùng (null). Mức SLA theo <see cref="PendingSince"/> + <see cref="TimeStatus"/>.</summary>
        public DateTime? DeadlineAt { get; set; }
        /// <summary>Mức ưu tiên theo thời gian đã chờ kể từ mốc gửi: OnTime (&lt;2 ngày), Warning (≥2), Critical (≥4), Overdue (≥7).</summary>
        public string? TimeStatus { get; set; }
        /// <summary>Admin: thời điểm moderator duyệt/từ chối.</summary>
        public DateTime? ReviewedAt { get; set; }
        /// <summary>Admin: tên moderator đã duyệt/từ chối.</summary>
        public string? ReviewedByModeratorName { get; set; }

        /// <summary>Ghi chú admin khi từ chối đơn RELEASE_ASSIGNMENT (hủy nhận duyệt) do moderator hiện tại gửi.</summary>
        public string? AdminRejectedReleaseNote { get; set; }
        /// <summary>Thời điểm admin từ chối đơn hủy nhận duyệt.</summary>
        public DateTime? AdminRejectedReleaseAt { get; set; }
    }
}