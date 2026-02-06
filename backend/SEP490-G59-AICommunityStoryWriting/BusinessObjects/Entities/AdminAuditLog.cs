using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AdminAuditLog
{
    public long Id { get; set; }

    public Guid? AdminId { get; set; }

    public string? ActionType { get; set; }

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? OldValue { get; set; }

    public string? NewValue { get; set; }

    public string? Reason { get; set; }

    public string? IpAddress { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? Admin { get; set; }
}
