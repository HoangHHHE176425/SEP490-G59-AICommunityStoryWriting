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
    }
}