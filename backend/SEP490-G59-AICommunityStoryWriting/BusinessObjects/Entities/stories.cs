using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class stories
{
    public Guid id { get; set; }

    public Guid? author_id { get; set; }

    public int? category_id { get; set; }

    public string title { get; set; } = null!;

    public string slug { get; set; } = null!;

    public string? cover_image { get; set; }

    public string? summary { get; set; }

    public string? status { get; set; }

    public DateTime? last_published_at { get; set; }

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

    public virtual ICollection<comments> comments { get; set; } = new List<comments>();

    public virtual ICollection<idea_posts> idea_posts { get; set; } = new List<idea_posts>();

    public virtual ICollection<purchases> purchases { get; set; } = new List<purchases>();

    public virtual ICollection<ratings> ratings { get; set; } = new List<ratings>();

    public virtual ICollection<story_commitments> story_commitments { get; set; } = new List<story_commitments>();

    public virtual ICollection<user_library> user_library { get; set; } = new List<user_library>();
}
