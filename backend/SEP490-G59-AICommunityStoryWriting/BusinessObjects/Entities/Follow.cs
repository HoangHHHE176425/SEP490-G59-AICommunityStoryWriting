using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Follow
{
    public Guid UserId { get; set; }

    public Guid AuthorId { get; set; }

    public DateTime? FollowedAt { get; set; }

    public virtual User Author { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
