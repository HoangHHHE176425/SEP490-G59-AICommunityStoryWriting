using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class StoryCommitment
{
    public Guid Id { get; set; }

    public Guid? StoryId { get; set; }

    public Guid? UserId { get; set; }

    public string? PolicyVersion { get; set; }

    public DateTime? SignedAt { get; set; }

    public string? IpAddress { get; set; }

    public virtual Story? Story { get; set; }

    public virtual User? User { get; set; }
}
