using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class comments
{
    public Guid id { get; set; }

    public Guid? user_id { get; set; }

    public Guid? story_id { get; set; }

    public Guid? chapter_id { get; set; }

    public Guid? parent_id { get; set; }

    public string content { get; set; } = null!;

    public int? likes_count { get; set; }

    public string? status { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual ICollection<comments> Inverseparent { get; set; } = new List<comments>();

    public virtual comments? parent { get; set; }

    public virtual stories? story { get; set; }

    public virtual users? userNavigation { get; set; }

    public virtual ICollection<users> user { get; set; } = new List<users>();
}
