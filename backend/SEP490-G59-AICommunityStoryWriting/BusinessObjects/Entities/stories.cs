using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class stories
{
    public string id { get; set; } = null!;

    public string? author_id { get; set; }

    public int? category_id { get; set; }

    public string title { get; set; } = null!;

    public string slug { get; set; } = null!;

    public string? cover_image { get; set; }

    public string? summary { get; set; }

    public string? status { get; set; }

    public string? sale_type { get; set; }

    public int? full_story_price { get; set; }

    public int? expected_chapters { get; set; }

    public int? release_frequency_days { get; set; }

    public DateTime? last_published_at { get; set; }

    public string? access_type { get; set; }

    public int? total_chapters { get; set; }

    public long? total_views { get; set; }

    public int? total_favorites { get; set; }

    public decimal? avg_rating { get; set; }

    public int? word_count { get; set; }

    public string? age_rating { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public DateTime? published_at { get; set; }

    public virtual users? author { get; set; }

    public virtual categories? category { get; set; }

    public virtual ICollection<chapters> chapters { get; set; } = new List<chapters>();
}
