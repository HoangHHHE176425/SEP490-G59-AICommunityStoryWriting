using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class IdeaProposal
{
    public Guid Id { get; set; }

    public Guid? PostId { get; set; }

    public Guid? UserId { get; set; }

    public string Content { get; set; } = null!;

    public int? VoteCount { get; set; }

    public bool? IsSelected { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual IdeaPost? Post { get; set; }
}
