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
        bool includeReplies);

    Task<Guid> RequestAdminActionAsync(
        Guid commentId,
        Guid requesterId,
        CreateComplianceAdminActionRequestDto dto,
        bool actorIsAdmin);

    Task<PagedComplianceCommentReportsDto> QueryComplianceOpenCommentReportsAsync(
        int page,
        int pageSize,
        string? statusCsv = null,
        string? search = null,
        Guid? actingUserId = null,
        bool viewerIsAdmin = false);

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
}

