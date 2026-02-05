using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class categories
{
    public Guid id { get; set; }

    public Guid? parent_id { get; set; }

    public string name { get; set; } = null!;

    public string slug { get; set; } = null!;

    public string? description { get; set; }

    public string? icon_url { get; set; }

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }

    public virtual ICollection<categories> Inverseparent_category { get; set; } = new List<categories>();

    public virtual categories? parent_category { get; set; }

    public virtual ICollection<stories> story { get; set; } = new List<stories>();
}
