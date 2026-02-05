using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ai_usage_logs
{
    public long id { get; set; }

    public Guid? user_id { get; set; }

    public Guid? story_id { get; set; }

    public Guid? chapter_id { get; set; }

    public string? action_type { get; set; }

    public string? model_name { get; set; }

    public int? prompt_tokens { get; set; }

    public int? completion_tokens { get; set; }

    public int? total_tokens { get; set; }

    public string? status { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? user { get; set; }
}
