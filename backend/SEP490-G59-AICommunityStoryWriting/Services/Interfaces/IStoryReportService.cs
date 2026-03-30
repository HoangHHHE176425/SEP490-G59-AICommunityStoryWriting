using Services.DTOs.StoryReports;

namespace Services.Interfaces;

public interface IStoryReportService
{
    IReadOnlyList<StoryReportReasonOptionDto> GetReasonOptions();

    Task<Guid> CreateStoryReportAsync(Guid storyId, Guid reporterId, CreateStoryReportRequestDto request);

    Task<PagedComplianceStoryReportsDto> QueryComplianceAsync(ComplianceStoryReportQueryDto query, Guid? actingUserId, bool viewerIsAdmin);

    Task<bool> UpdateReportStatusAsync(Guid reportId, Guid actorId, string newStatus, bool actorIsAdmin);

    /// <summary>COMPLIANCE đang lock truyện: đánh dấu một báo cáo RESOLVED/DISMISSED.</summary>
    Task<bool> ComplianceResolveReportAsync(Guid reportId, Guid complianceUserId, ComplianceResolveReportRequestDto? dto);

    /// <summary>COMPLIANCE đang lock: đóng mọi báo cáo NEW/IN_REVIEW của truyện.</summary>
    Task<int> ComplianceResolveOpenReportsForStoryAsync(Guid storyId, Guid complianceUserId, ComplianceResolveReportRequestDto? dto);

    /// <summary>Lịch sử báo cáo do chính compliance này đánh dấu đã xử lý.</summary>
    Task<PagedComplianceStoryReportsDto> QueryMyResolvedComplianceReportsAsync(int page, int pageSize, Guid complianceUserId, string? search);

    /// <summary>Lock truyện qua review_assignments; không sửa hàng loạt status reports. Trả về số báo cáo NEW/IN_REVIEW hiện có.</summary>
    Task<ComplianceClaimStoryResultDto> ClaimStoryAsync(Guid storyId, Guid complianceUserId);

    /// <summary>Chỉ ADMIN: gỡ lock + (tuỳ) đưa báo cáo IN_REVIEW của người cũ về NEW.</summary>
    Task<int> ReleaseComplianceStoryClaimAsync(Guid storyId, Guid adminUserId, bool actorIsAdmin);

    /// <summary>Compliance đang giữ lock gửi yêu cầu admin gỡ / giao lại.</summary>
    Task<Guid> RequestComplianceLockReleaseAsync(Guid storyId, Guid requesterId, RequestComplianceLockReleaseDto? dto);

    Task<IReadOnlyList<ComplianceLockRequestListItemDto>> AdminListComplianceLockRequestsAsync(string? status);

    /// <summary>COMPLIANCE/ADMIN: đơn gỡ lock do chính mình gửi (mọi trạng thái).</summary>
    Task<IReadOnlyList<ComplianceLockRequestListItemDto>> ListMyComplianceLockRequestsAsync(Guid requesterId);

    Task<IReadOnlyList<ComplianceOfficerAssignmentOptionDto>> AdminListComplianceOfficersForAssignmentAsync();

    Task AdminResolveComplianceLockRequestAsync(Guid requestId, Guid adminId, AdminResolveComplianceLockRequestDto dto);

    Task SetStoryComplianceFlagAsync(Guid storyId, Guid actorId, bool flagged, string? note, bool actorIsAdmin);

    Task SetStoryCommentsDisabledAsync(Guid storyId, Guid actorId, bool disabled, bool actorIsAdmin);

    Task SetStoryComplianceHiddenAsync(Guid storyId, Guid actorId, bool hidden, bool actorIsAdmin);

    Task<Guid> RequestComplianceAdminActionAsync(Guid storyId, Guid requesterId, CreateComplianceAdminActionRequestDto dto, bool actorIsAdmin);

    /// <summary>ADMIN hoặc COMPLIANCE xem lịch sử violation_logs của user.</summary>
    Task<IReadOnlyList<ViolationLogListItemDto>> ListViolationsForUserAsync(Guid violatorUserId, int take, bool viewerIsComplianceOrAdmin);

    Task<IReadOnlyList<ComplianceAdminActionRequestListItemDto>> AdminListComplianceAdminActionRequestsAsync(string? status);

    /// <summary>COMPLIANCE/ADMIN: đơn BAN/SUSPEND do chính mình gửi (mọi trạng thái).</summary>
    Task<IReadOnlyList<ComplianceAdminActionRequestListItemDto>> ListMyComplianceAdminActionRequestsAsync(Guid requesterId);

    Task AdminResolveComplianceAdminActionRequestAsync(Guid requestId, Guid adminId, AdminResolveComplianceAdminActionRequestDto dto);
}
