using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Entities;

[Index("slug", Name = "UQ__stories__32DD1E4C10A5D55B", IsUnique = true)]
public partial class story
{
    [Key]
    public int id { get; set; }

    public int? author_id { get; set; }

    public int? category_id { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string title { get; set; } = null!;

    [StringLength(255)]
    [Unicode(false)]
    public string slug { get; set; } = null!;

    public string? cover_image { get; set; }

    public string? summary { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? status { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? sale_type { get; set; }

    public int? full_story_price { get; set; }

    public int? expected_chapters { get; set; }

    public int? release_frequency_days { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? last_published_at { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? access_type { get; set; }

    public int? total_chapters { get; set; }

    public long? total_views { get; set; }

    public int? total_favorites { get; set; }

    [Column(TypeName = "decimal(3, 2)")]
    public decimal? avg_rating { get; set; }

    public int? word_count { get; set; }

    [StringLength(10)]
    [Unicode(false)]
    public string? age_rating { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? updated_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? published_at { get; set; }

    [ForeignKey("author_id")]
    [InverseProperty("stories")]
    public virtual user? author { get; set; }

    [ForeignKey("category_id")]
    [InverseProperty("stories")]
    public virtual category? category { get; set; }

    [InverseProperty("story")]
    public virtual ICollection<chapter> chapters { get; set; } = new List<chapter>();
}
