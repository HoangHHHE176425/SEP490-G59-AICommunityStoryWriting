using Services.DTOs.Moderation;
using Services.DTOs.StoryReports;

namespace Services.DTOs.Admin;

public class AdminUnifiedEscalationPendingResponseDto
{
    public List<AdminUnifiedEscalationPendingItemDto> Items { get; set; } = new();

    public int Critical { get; set; }
    public int High { get; set; }
    public int Standard { get; set; }
}

/// <summary>Một dòng trong danh sách đơn chờ admin — moderator hoặc compliance (gồm 2 loại).</summary>
public class AdminUnifiedEscalationPendingItemDto
{
    /// <summary>REVIEW_ESCALATION | COMPLIANCE_LOCK | COMPLIANCE_ADMIN_ACTION</summary>
    public string Source { get; set; } = null!;

    public string UrgencyTier { get; set; } = null!;

    public ReviewEscalationListItemDto? ModeratorEscalation { get; set; }

    public ComplianceLockRequestListItemDto? ComplianceLock { get; set; }

    public ComplianceAdminActionRequestListItemDto? ComplianceAdminAction { get; set; }
}
