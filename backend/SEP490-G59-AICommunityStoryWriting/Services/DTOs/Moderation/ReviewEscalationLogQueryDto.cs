namespace Services.DTOs.Moderation
{
    /// <summary>Bộ lọc + phân trang cho log đơn escalation (admin).</summary>
    public class ReviewEscalationLogQueryDto
    {
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;

        /// <summary>Tìm theo lý do, tiêu đề story/chapter, hoặc đúng GUID (id / target / sender / resolver).</summary>
        public string? Search { get; set; }

        /// <summary>PENDING | APPROVED | REJECTED — để trống = tất cả.</summary>
        public string? Status { get; set; }

        /// <summary>EXTEND_DEADLINE | RELEASE_ASSIGNMENT — để trống = tất cả.</summary>
        public string? RequestKind { get; set; }

        /// <summary>STORY | CHAPTER — để trống = tất cả.</summary>
        public string? TargetType { get; set; }

        public Guid? SenderId { get; set; }
        public Guid? ResolverId { get; set; }

        public DateTime? CreatedFrom { get; set; }
        public DateTime? CreatedTo { get; set; }
        public DateTime? ResolvedFrom { get; set; }
        public DateTime? ResolvedTo { get; set; }

        /// <summary>created_at | resolved_at</summary>
        public string? SortBy { get; set; }

        /// <summary>asc | desc</summary>
        public string? SortOrder { get; set; }
    }
}
