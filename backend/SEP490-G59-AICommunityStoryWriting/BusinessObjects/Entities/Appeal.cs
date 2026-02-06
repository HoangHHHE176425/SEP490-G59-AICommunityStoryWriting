using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Appeal
{
    public Guid Id { get; set; }

    public Guid? ViolationId { get; set; }

    public Guid? UserId { get; set; }

    public string Content { get; set; } = null!;

    public string? EvidenceUrl { get; set; }

    public string? Status { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }

    public virtual ViolationLog? Violation { get; set; }
}
