using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class idea_posts
{
    public Guid id { get; set; }

    public Guid? story_id { get; set; }

    public Guid? author_id { get; set; }

    public string topic { get; set; } = null!;

    public string? description { get; set; }

    public int? reward_coin { get; set; }

    public string? status { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? expires_at { get; set; }

    public virtual ICollection<idea_proposals> idea_proposals { get; set; } = new List<idea_proposals>();

    public virtual stories? story { get; set; }
}
