using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ViolationLog
{
    public Guid Id { get; set; }

    public Guid? ComplianceOfficerId { get; set; }

    public Guid? ViolatorId { get; set; }

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? PenaltyType { get; set; }

    public string? Reason { get; set; }

    public string? PolicyReference { get; set; }

    public bool? IsRefunded { get; set; }

    public int? TotalRefundedAmount { get; set; }

    public bool? IsAppealed { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Appeal> Appeals { get; set; } = new List<Appeal>();

    public virtual User? ComplianceOfficer { get; set; }

    public virtual User? Violator { get; set; }
}
