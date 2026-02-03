using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Entities;

[Index("story_id", "order_index", Name = "uq_story_chapter", IsUnique = true)]
public partial class chapter
{
    [Key]
    public int id { get; set; }

    public int story_id { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string title { get; set; } = null!;

    public int order_index { get; set; }

    public string? content { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? status { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? access_type { get; set; }

    public int? coin_price { get; set; }

    public int? word_count { get; set; }

    [Column(TypeName = "decimal(5, 2)")]
    public decimal? ai_contribution_ratio { get; set; }

    public bool? is_ai_clean { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? published_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? updated_at { get; set; }

    [ForeignKey("story_id")]
    [InverseProperty("chapters")]
    public virtual story story { get; set; } = null!;
}
