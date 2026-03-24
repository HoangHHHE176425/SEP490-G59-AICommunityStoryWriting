namespace Services.DTOs.Stories
{
    public class StoryResponseDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = null!;
        public string Slug { get; set; } = null!;
        public string? Summary { get; set; }

        public List<Guid> CategoryIds { get; set; } = new();
        public string? CategoryNames { get; set; }
        public Guid? AuthorId { get; set; }
        public string? AuthorName { get; set; }
        /// <summary>Đường dẫn avatar tác giả (user_profiles.avatar_url).</summary>
        public string? AuthorAvatarUrl { get; set; }

        public string? CoverImage { get; set; }
        public string? Status { get; set; }
        /// <summary>ONGOING = Đang ra, COMPLETED = Hoàn thành, HIATUS = Tạm dừng</summary>
        public string? StoryProgressStatus { get; set; }
        public string? AgeRating { get; set; }

        public int? TotalChapters { get; set; }
        /// <summary>Số chapter đã PUBLISHED.</summary>
        public int? PublishedChaptersCount { get; set; }
        public long? TotalViews { get; set; }
        public int? TotalComments { get; set; }
        public int? TotalFavorites { get; set; }
        public decimal? AvgRating { get; set; }
        public int? WordCount { get; set; }

        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        /// <summary>Thời gian cập nhật gần nhất (max giữa story.updated_at và chapter.updated_at mới nhất).</summary>
        public DateTime? LatestUpdatedAt { get; set; }
        public DateTime? PublishedAt { get; set; }
        public DateTime? LastPublishedAt { get; set; }

        /// <summary>Lý do từ chối (có khi status = REJECTED).</summary>
        public string? RejectionReason { get; set; }
        /// <summary>Thời điểm moderator từ chối.</summary>
        public DateTime? RejectedAt { get; set; }

        /// <summary>User hiện tại đã theo dõi story này chưa (chỉ có khi đăng nhập).</summary>
        public bool? UserIsFollowing { get; set; }

        /// <summary>Chương đang đọc dở (id) - có khi user đã đăng nhập và từng đọc truyện này.</summary>
        public Guid? LastReadChapterId { get; set; }
        /// <summary>Tiêu đề chương đang đọc dở - để hiển thị "Đọc tiếp chương X".</summary>
        public string? LastReadChapterTitle { get; set; }
        /// <summary>Thời điểm đọc chương đó lần cuối.</summary>
        public DateTime? LastReadAt { get; set; }

        public bool CommentsDisabled { get; set; }
        public bool ComplianceHidden { get; set; }
        public bool ComplianceFlagged { get; set; }
    }
}