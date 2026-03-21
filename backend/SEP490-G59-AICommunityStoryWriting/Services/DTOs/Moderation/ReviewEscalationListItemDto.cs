namespace Services.DTOs.Moderation
{
    public class ReviewEscalationListItemDto
    {
        public Guid Id { get; set; }
        public string TargetType { get; set; } = null!;
        public Guid TargetId { get; set; }
        public string? TargetTitle { get; set; }
        public string RequestKind { get; set; } = null!;
        public string Reason { get; set; } = null!;
        public DateTime? ProposedDeadlineAt { get; set; }
        public string Status { get; set; } = null!;
        public DateTime CreatedAt { get; set; }
        public Guid SenderId { get; set; }
        public string? SenderName { get; set; }
        public DateTime? CurrentAssignmentDeadlineAt { get; set; }
        /// <summary>Thời điểm tác giả gửi duyệt (UTC) — hạn giao lại không được sớm hơn mốc này.</summary>
        public DateTime? AuthorSubmittedAtUtc { get; set; }
        /// <summary>CRITICAL | HIGH | STANDARD — mức cần xử lý gấp (tính khi còn PENDING)</summary>
        public string UrgencyTier { get; set; } = null!;

        public Guid? ResolverId { get; set; }
        public string? ResolverName { get; set; }
        public string? ResolverNote { get; set; }
        public DateTime? ResolvedAt { get; set; }
        /// <summary>Hạn xác nhận sau khi admin xử lý (gia hạn / giao lại).</summary>
        public DateTime? ConfirmedDeadlineAt { get; set; }
    }

    public class ReviewAssignmentSelfDto
    {
        public bool IsAssignedToMe { get; set; }
        /// <summary>Hạn moderator phải hoàn thành (khi đã nhận duyệt).</summary>
        public DateTime? ReviewDeadlineAt { get; set; }
        public bool HasPendingEscalation { get; set; }

        /// <summary>Thời điểm tác giả gửi bản chờ duyệt (UTC) — dùng hiển thị “đã gửi … trước”.</summary>
        public DateTime? AuthorSubmittedAtUtc { get; set; }

        /// <summary>Tham chiếu nội bộ: mốc +7 ngày sau lúc tác giả gửi (gia hạn / quy tắc admin).</summary>
        public DateTime? PolicySuggestedDeadlineAt { get; set; }

        /// <summary>OnTime | Warning | Critical | Overdue — theo thời gian đã chờ từ mốc tác giả gửi.</summary>
        public string? TimeStatus { get; set; }
    }
}
