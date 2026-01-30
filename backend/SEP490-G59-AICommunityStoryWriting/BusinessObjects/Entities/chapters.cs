using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class chapters
{
    public string id { get; set; } = null!;

    public string story_id { get; set; } = null!;

    public string title { get; set; } = null!;

    public int order_index { get; set; }

    public string? content { get; set; }

    public string? status { get; set; }

    public string? access_type { get; set; }

    public int? coin_price { get; set; }

    public int? word_count { get; set; }

    public decimal? ai_contribution_ratio { get; set; }

    public bool? is_ai_clean { get; set; }

    public DateTime? published_at { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual stories story { get; set; } = null!;
}
