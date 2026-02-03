using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class purchases
{
    public Guid id { get; set; }

    public Guid? user_id { get; set; }

    public Guid? story_id { get; set; }

    public Guid? chapter_id { get; set; }

    public int price_paid { get; set; }

    public string? purchase_type { get; set; }

    public string? escrow_status { get; set; }

    public DateTime? released_at { get; set; }

    public decimal? platform_fee_ratio { get; set; }

    public DateTime? created_at { get; set; }

    public virtual stories? story { get; set; }

    public virtual users? user { get; set; }
}
