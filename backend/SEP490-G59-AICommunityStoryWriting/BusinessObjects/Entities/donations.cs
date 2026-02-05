using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class donations
{
    public Guid id { get; set; }

    public Guid? sender_id { get; set; }

    public Guid? receiver_id { get; set; }

    public Guid? story_id { get; set; }

    public int amount { get; set; }

    public string? message { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? receiver { get; set; }

    public virtual users? sender { get; set; }
}
