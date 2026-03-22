namespace Services.DTOs.StoryReports;

/// <summary>APPROVE_UNLOCK | APPROVE_REASSIGN | REJECT</summary>
public class AdminResolveComplianceLockRequestDto
{
    public string Decision { get; set; } = "";

    public Guid? NewAssigneeId { get; set; }

    public DateTime? ReviewDeadlineAtUtc { get; set; }

    public string? AdminNote { get; set; }
}
