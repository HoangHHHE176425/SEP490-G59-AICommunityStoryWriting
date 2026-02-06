using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Report
{
    public Guid Id { get; set; }

    public Guid? ReporterId { get; set; }

    public string? TargetType { get; set; }

    public Guid TargetId { get; set; }

    public string? ReasonCategory { get; set; }

    public string? Description { get; set; }

    public string? Status { get; set; }

    public Guid? AssignedTo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ResolvedAt { get; set; }

    public virtual User? AssignedToNavigation { get; set; }

    public virtual ICollection<ReportEvidence> ReportEvidences { get; set; } = new List<ReportEvidence>();

    public virtual User? Reporter { get; set; }
}
