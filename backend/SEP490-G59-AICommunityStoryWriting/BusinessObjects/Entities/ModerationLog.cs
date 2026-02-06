using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ModerationLog
{
    public long Id { get; set; }

    public Guid? ModeratorId { get; set; }

    public string? TargetType { get; set; }

    public Guid? TargetId { get; set; }

    public string? Action { get; set; }

    public string? RejectionReason { get; set; }

    public int? ProcessingTimeMs { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? Moderator { get; set; }
}
