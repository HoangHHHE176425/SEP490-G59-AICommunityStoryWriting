using Services.DTOs.CommentReports;
using Services.DTOs.StoryReports;

namespace Services.Interfaces;

public interface ICommentReportService
{
    IReadOnlyList<StoryReportReasonOptionDto> GetReasonOptions();

    Task<Guid> CreateCommentReportAsync(
        Guid commentId,
        Guid reporterId,
        CreateCommentReportRequestDto request,
        Guid? expectedStoryId = null,
        Guid? expectedChapterId = null);

    Task<bool> ComplianceResolveReportAsync(
        Guid reportId,
        Guid complianceUserId,
        ComplianceResolveCommentReportRequestDto? dto,
        bool actorIsAdmin);

    Task SetCommentThreadHiddenAsync(
        Guid commentId,
        Guid actorUserId,
        bool hidden,
        bool includeReplies,
        bool actorIsAdmin = false);

    Task<Guid> RequestAdminActionAsync(
        Guid commentId,
        Guid requesterId,
        CreateComplianceAdminActionRequestDto dto,
        bool actorIsAdmin);

    /// <summary>COMPLIANCE (hoặc ADMIN) đang lock thread: bật/tắt tạm khóa quyền viết, không qua đơn admin.</summary>
    Task SetAuthorWritingSuspendedByComplianceAsync(
        Guid commentId,
        Guid actorUserId,
        SetComplianceCommentAuthorWritingSuspendedRequestDto dto,
        bool actorIsAdmin);

    /// <summary>COMPLIANCE đang lock thread: gửi admin yêu cầu gỡ lock (sau khi gửi, thread bị chặn mọi thao tác tới khi admin xử lý).</summary>
    Task<Guid> RequestComplianceCommentLockReleaseAsync(
        Guid commentId,
        Guid requesterId,
        RequestComplianceLockReleaseDto? dto);

    Task<PagedComplianceCommentReportsDto> QueryComplianceOpenCommentReportsAsync(
        int page,
        int pageSize,
        string? statusCsv = null,
        string? search = null,
        Guid? actingUserId = null,
        bool viewerIsAdmin = false,
        string? claimFilter = null);

    Task<ComplianceClaimCommentResultDto> ClaimCommentReportsAsync(
        Guid commentId,
        Guid complianceUserId);

    Task<int> ReleaseComplianceCommentClaimAsync(
        Guid commentId,
        Guid adminUserId);

    /// <summary>COMPLIANCE: đóng toàn bộ report comment đang mở của 1 comment thread.</summary>
    Task<int> ComplianceResolveAllOpenCommentReportsAsync(
        Guid commentId,
        Guid complianceUserId,
        ComplianceResolveCommentReportRequestDto? dto,
        bool actorIsAdmin);

    /// <summary>COMPLIANCE: đánh dấu / gỡ đánh dấu xác minh cho từng request báo cáo (report_evidences) trong thread.</summary>
    Task<int> SetComplianceCommentReportEvidenceVerifiedAsync(
        Guid commentId,
        Guid actorUserId,
        SetComplianceCommentReportEvidenceVerifiedRequestDto dto,
        bool actorIsAdmin);
}

