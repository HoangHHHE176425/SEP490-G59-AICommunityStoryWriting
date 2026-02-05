using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ai_generated_content
{
    public Guid id { get; set; }

    public Guid? chapter_id { get; set; }

    public Guid? user_id { get; set; }

    public string? input_prompt { get; set; }

    public string? ai_output { get; set; }

    public decimal? similarity_score { get; set; }

    public DateTime? created_at { get; set; }

    public virtual chapters? chapter { get; set; }
}
