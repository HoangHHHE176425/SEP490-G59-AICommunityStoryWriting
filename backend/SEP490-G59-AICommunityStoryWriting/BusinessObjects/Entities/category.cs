using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Entities;

[Index("slug", Name = "UQ__categori__32DD1E4C35344188", IsUnique = true)]
public partial class category
{
    [Key]
    public int id { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string name { get; set; } = null!;

    [StringLength(100)]
    [Unicode(false)]
    public string slug { get; set; } = null!;

    public string? description { get; set; }

    public string? icon_url { get; set; }

    public bool? is_active { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? created_at { get; set; }

    [InverseProperty("category")]
    public virtual ICollection<story> stories { get; set; } = new List<story>();
}
