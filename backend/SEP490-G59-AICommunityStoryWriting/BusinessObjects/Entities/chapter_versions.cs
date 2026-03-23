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

    public string status { get; set; } = null!;

    public Guid? reviewed_by { get; set; }

    public DateTime? reviewed_at { get; set; }

    public string? rejection_reason { get; set; }

    public string? title_snapshot { get; set; }

    /// <summary>% giống nội dung AI (0–100), cập nhật khi gọi API so sánh snapshot với ai_generated_content.</summary>
    public decimal? ai_similarity_percent { get; set; }

    public virtual users? author { get; set; }

    public virtual chapters? chapter { get; set; }
}
