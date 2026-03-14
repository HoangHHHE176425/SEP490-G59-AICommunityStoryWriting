using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class story_event_memory
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    public Guid? chapter_id { get; set; }

    public int order_index { get; set; }

    public string description { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public virtual chapters? chapter { get; set; }

    public virtual stories story { get; set; } = null!;
}
