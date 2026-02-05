using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class chapter_versions
{
    public Guid id { get; set; }

    public Guid? chapter_id { get; set; }

    public Guid? author_id { get; set; }

    public string? content_snapshot { get; set; }

    public int version_number { get; set; }

    public string? change_summary { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? author { get; set; }

    public virtual chapters? chapter { get; set; }
}
