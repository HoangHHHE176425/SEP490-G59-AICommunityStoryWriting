namespace Services.DTOs.Moderation
{
    public class AdminResolveReviewEscalationDto
    {
        /// <summary>true = chấp nhận yêu cầu, false = từ chối</summary>
        public bool Approve { get; set; }

        /// <summary>Ghi chú admin. Khi <see cref="Approve"/> = false: bắt buộc, tối thiểu 10 ký tự (sau trim), tối đa 2000 ký tự — đồng bộ validate trong <c>ReviewEscalationService.Resolve</c>.</summary>
        public string? AdminNote { get; set; }

        /// <summary>Khi duyệt EXTEND: có thể chỉnh hạn; null = dùng proposed_deadline của moderator</summary>
        public DateTime? ConfirmedDeadlineAt { get; set; }

        /// <summary>Khi duyệt RELEASE: null = trả mục về hàng đợi (chưa ai nhận); có giá trị = giao lock cho moderator đó (bắt buộc kèm <see cref="ConfirmedDeadlineAt"/>).</summary>
        public Guid? ReassignToUserId { get; set; }
    }
}
