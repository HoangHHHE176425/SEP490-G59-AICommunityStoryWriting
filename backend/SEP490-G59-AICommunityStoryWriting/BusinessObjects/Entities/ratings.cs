using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ratings
{
    public Guid id { get; set; }

    public Guid? user_id { get; set; }

    public Guid? story_id { get; set; }

    public int? star_value { get; set; }

    public string? review_text { get; set; }

    public string? status { get; set; }

    public DateTime? created_at { get; set; }

    public virtual stories? story { get; set; }

    public virtual users? user { get; set; }
}
