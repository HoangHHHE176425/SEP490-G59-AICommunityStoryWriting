using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class IdeaPost
{
    public Guid Id { get; set; }

    public Guid? StoryId { get; set; }

    public Guid? AuthorId { get; set; }

    public string Topic { get; set; } = null!;

    public string? Description { get; set; }

    public int? RewardCoin { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ExpiresAt { get; set; }

    public virtual ICollection<IdeaProposal> IdeaProposals { get; set; } = new List<IdeaProposal>();

    public virtual Story? Story { get; set; }
}
