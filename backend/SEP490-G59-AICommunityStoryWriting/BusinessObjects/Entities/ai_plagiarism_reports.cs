using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ai_plagiarism_reports
{
    public Guid id { get; set; }

    public Guid? chapter_id { get; set; }

    public decimal? similarity_ratio { get; set; }

    public string? source_details { get; set; }

    public string? status { get; set; }

    public DateTime? checked_at { get; set; }

    public virtual chapters? chapter { get; set; }
}
