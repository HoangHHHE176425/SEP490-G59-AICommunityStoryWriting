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
        /// <summary>CRITICAL | STANDARD — trả về API sau khi gộp HIGH → STANDARD; nguồn: sender_urgency_tier hoặc suy ra từ hạn assignment / tuổi đơn.</summary>
        public string UrgencyTier { get; set; } = null!;

        public Guid? ResolverId { get; set; }
        public string? ResolverName { get; set; }
        public string? ResolverNote { get; set; }
        public DateTime? ResolvedAt { get; set; }
        /// <summary>Hạn xác nhận sau khi admin xử lý (gia hạn / giao lại).</summary>
        public DateTime? ConfirmedDeadlineAt { get; set; }

        /// <summary>
        /// RELEASE_ASSIGNMENT + STORY: id các chương đang được moderator gửi đơn claim (thứ tự order_index tăng dần).
        /// Admin dùng để chỉ hiển thị đúng phạm vi hủy nhận duyệt.
        /// </summary>
        public List<Guid>? ReleaseAffectedChapterIds { get; set; }
    }

    public class ReviewAssignmentSelfDto
    {
        public bool IsAssignedToMe { get; set; }
        /// <summary>Hạn moderator phải hoàn thành (khi đã nhận duyệt).</summary>
        public DateTime? ReviewDeadlineAt { get; set; }
        public bool HasPendingEscalation { get; set; }

        /// <summary>False khi đã gửi đủ 1 đơn xin gia hạn trong phiên nhận duyệt hiện tại (vẫn có thể gửi hủy nhận).</summary>
        public bool CanSubmitExtendDeadlineRequest { get; set; }

        /// <summary>Thời điểm tác giả gửi bản chờ duyệt (UTC) — dùng hiển thị “đã gửi … trước”.</summary>
        public DateTime? AuthorSubmittedAtUtc { get; set; }

        /// <summary>Tham chiếu nội bộ: mốc +7 ngày sau lúc tác giả gửi (gia hạn / quy tắc admin).</summary>
        public DateTime? PolicySuggestedDeadlineAt { get; set; }

        /// <summary>OnTime | Warning | Critical | Overdue — theo thời gian đã chờ từ mốc tác giả gửi.</summary>
        public string? TimeStatus { get; set; }
    }
}
