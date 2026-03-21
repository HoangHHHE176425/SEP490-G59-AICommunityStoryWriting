using Services.DTOs.Admin;
using Services.DTOs.Moderation;

namespace Services.Interfaces
{
    public interface IReviewEscalationService
    {
        ReviewAssignmentSelfDto GetSelfAssignment(string targetType, Guid targetId, Guid userId);

        /// <summary>Người gửi (sender) — thường là moderator đang nhận duyệt; có thể mở rộng role khác theo request_kind.</summary>
        Guid Submit(Guid senderId, ModeratorSubmitReviewEscalationDto dto);

        IReadOnlyList<ReviewEscalationListItemDto> ListPendingForAdmin(string? urgencyTier = null);

        /// <summary>Đơn APPROVED/REJECTED để xem lại lịch sử.</summary>
        IReadOnlyList<ReviewEscalationListItemDto> ListResolvedHistoryForAdmin(int skip = 0, int take = 200);

        int CountResolvedHistory();

        /// <summary>Log đầy đủ: lọc, tìm kiếm, phân trang.</summary>
        PagedResultDto<ReviewEscalationListItemDto> SearchEscalationLogForAdmin(ReviewEscalationLogQueryDto query);

        (int critical, int high, int standard) CountPendingUrgencyBuckets();

        void Resolve(Guid resolverId, Guid requestId, AdminResolveReviewEscalationDto dto);
    }
}
