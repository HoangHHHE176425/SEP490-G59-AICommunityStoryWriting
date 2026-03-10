using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class chapters
{
    public Guid id { get; set; }

    public Guid? story_id { get; set; }

    public string title { get; set; } = null!;

    public int order_index { get; set; }

    public string? content { get; set; }

    public string? status { get; set; }

    public string? access_type { get; set; }

    public int? coin_price { get; set; }

    public int? word_count { get; set; }

    public decimal? ai_contribution_ratio { get; set; }

    /// <summary>Phần trăm giống với bản AI (0–100), lưu khi chương đã PUBLISHED và gọi compare-chapter.</summary>
    public decimal? ai_similarity_percent { get; set; }

    public bool? is_ai_clean { get; set; }

    public DateTime? published_at { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual ICollection<ai_generated_content> ai_generated_content { get; set; } = new List<ai_generated_content>();

    public virtual ICollection<ai_plagiarism_reports> ai_plagiarism_reports { get; set; } = new List<ai_plagiarism_reports>();

    public virtual ICollection<chapter_versions> chapter_versions { get; set; } = new List<chapter_versions>();

    public virtual ICollection<story_event_memory> story_event_memories { get; set; } = new List<story_event_memory>();

    public virtual stories? story { get; set; }
}
