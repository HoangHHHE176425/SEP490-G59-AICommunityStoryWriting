using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class marketing_banners
{
    public int id { get; set; }

    public string? title { get; set; }

    public string image_url { get; set; } = null!;

    public string? link_url { get; set; }

    public string? position { get; set; }

    public int? priority { get; set; }

    public DateTime? start_date { get; set; }

    public DateTime? end_date { get; set; }

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }
}
