using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class story_versions
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    public Guid author_id { get; set; }

    public string? title_snapshot { get; set; }

    public string? summary_snapshot { get; set; }

    public string? cover_image_snapshot { get; set; }

    public string? status_snapshot { get; set; }

    public int version_number { get; set; }

    public string? change_summary { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users author { get; set; } = null!;

    public virtual stories story { get; set; } = null!;
}
