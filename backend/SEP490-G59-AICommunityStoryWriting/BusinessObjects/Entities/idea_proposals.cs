using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class idea_proposals
{
    public Guid id { get; set; }

    public Guid? post_id { get; set; }

    public Guid? user_id { get; set; }

    public string content { get; set; } = null!;

    public int? vote_count { get; set; }

    public bool? is_selected { get; set; }

    public string? status { get; set; }

    public DateTime? created_at { get; set; }

    public virtual idea_posts? post { get; set; }
}
