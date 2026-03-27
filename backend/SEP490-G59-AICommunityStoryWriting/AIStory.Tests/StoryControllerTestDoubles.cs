using Services.DTOs.Notifications;
using Services.DTOs.StoryReports;
using Services.Interfaces;

namespace AIStory.Tests;

internal sealed class NoOpNotificationHubNotifier : INotificationHubNotifier
{
    public Task NotifyUserAsync(Guid userId, NotificationDto notification) => Task.CompletedTask;
}

internal sealed class StubStoryReportService : IStoryReportService
{
    public IReadOnlyList<StoryReportReasonOptionDto> GetReasonOptions() => Array.Empty<StoryReportReasonOptionDto>();

    public Task<Guid> CreateStoryReportAsync(Guid storyId, Guid reporterId, CreateStoryReportRequestDto request) =>
        Task.FromResult(Guid.NewGuid());

    public Task<PagedComplianceStoryReportsDto> QueryComplianceAsync(ComplianceStoryReportQueryDto query, Guid? actingUserId, bool viewerIsAdmin) =>
        Task.FromResult(new PagedComplianceStoryReportsDto());

    public Task<bool> UpdateReportStatusAsync(Guid reportId, Guid actorId, string newStatus, bool actorIsAdmin) =>
        Task.FromResult(false);

    public Task<bool> ComplianceResolveReportAsync(Guid reportId, Guid complianceUserId, ComplianceResolveReportRequestDto? dto) =>
        Task.FromResult(false);

    public Task<int> ComplianceResolveOpenReportsForStoryAsync(Guid storyId, Guid complianceUserId, ComplianceResolveReportRequestDto? dto) =>
        Task.FromResult(0);

    public Task<PagedComplianceStoryReportsDto> QueryMyResolvedComplianceReportsAsync(int page, int pageSize, Guid complianceUserId, string? search) =>
        Task.FromResult(new PagedComplianceStoryReportsDto());

    public Task<ComplianceClaimStoryResultDto> ClaimStoryAsync(Guid storyId, Guid complianceUserId) =>
        Task.FromResult(new ComplianceClaimStoryResultDto());

    public Task<int> ReleaseComplianceStoryClaimAsync(Guid storyId, Guid adminUserId, bool actorIsAdmin) =>
        Task.FromResult(0);

    public Task<Guid> RequestComplianceLockReleaseAsync(Guid storyId, Guid requesterId, RequestComplianceLockReleaseDto? dto) =>
        Task.FromResult(Guid.NewGuid());

    public Task<IReadOnlyList<ComplianceLockRequestListItemDto>> AdminListComplianceLockRequestsAsync(string? status) =>
        Task.FromResult<IReadOnlyList<ComplianceLockRequestListItemDto>>(Array.Empty<ComplianceLockRequestListItemDto>());

    public Task<IReadOnlyList<ComplianceLockRequestListItemDto>> ListMyComplianceLockRequestsAsync(Guid requesterId) =>
        Task.FromResult<IReadOnlyList<ComplianceLockRequestListItemDto>>(Array.Empty<ComplianceLockRequestListItemDto>());

    public Task<IReadOnlyList<ComplianceOfficerAssignmentOptionDto>> AdminListComplianceOfficersForAssignmentAsync() =>
        Task.FromResult<IReadOnlyList<ComplianceOfficerAssignmentOptionDto>>(Array.Empty<ComplianceOfficerAssignmentOptionDto>());

    public Task AdminResolveComplianceLockRequestAsync(Guid requestId, Guid adminId, AdminResolveComplianceLockRequestDto dto) =>
        Task.CompletedTask;

    public Task SetStoryComplianceFlagAsync(Guid storyId, Guid actorId, bool flagged, string? note, bool actorIsAdmin) =>
        Task.CompletedTask;

    public Task SetStoryCommentsDisabledAsync(Guid storyId, Guid actorId, bool disabled, bool actorIsAdmin) =>
        Task.CompletedTask;

    public Task SetStoryComplianceHiddenAsync(Guid storyId, Guid actorId, bool hidden, bool actorIsAdmin) =>
        Task.CompletedTask;

    public Task<Guid> RequestComplianceAdminActionAsync(Guid storyId, Guid requesterId, CreateComplianceAdminActionRequestDto dto, bool actorIsAdmin) =>
        Task.FromResult(Guid.NewGuid());

    public Task<IReadOnlyList<ViolationLogListItemDto>> ListViolationsForUserAsync(Guid violatorUserId, int take, bool viewerIsComplianceOrAdmin) =>
        Task.FromResult<IReadOnlyList<ViolationLogListItemDto>>(Array.Empty<ViolationLogListItemDto>());

    public Task<IReadOnlyList<ComplianceAdminActionRequestListItemDto>> AdminListComplianceAdminActionRequestsAsync(string? status) =>
        Task.FromResult<IReadOnlyList<ComplianceAdminActionRequestListItemDto>>(Array.Empty<ComplianceAdminActionRequestListItemDto>());

    public Task<IReadOnlyList<ComplianceAdminActionRequestListItemDto>> ListMyComplianceAdminActionRequestsAsync(Guid requesterId) =>
        Task.FromResult<IReadOnlyList<ComplianceAdminActionRequestListItemDto>>(Array.Empty<ComplianceAdminActionRequestListItemDto>());

    public Task AdminResolveComplianceAdminActionRequestAsync(Guid requestId, Guid adminId, AdminResolveComplianceAdminActionRequestDto dto) =>
        Task.CompletedTask;
}
