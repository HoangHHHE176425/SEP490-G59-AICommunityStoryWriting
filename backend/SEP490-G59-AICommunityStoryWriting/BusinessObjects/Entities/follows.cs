using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class follows
{
    public Guid user_id { get; set; }

    public Guid author_id { get; set; }

    public DateTime? followed_at { get; set; }

    public virtual users author { get; set; } = null!;

    public virtual users user { get; set; } = null!;
}
