using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Entities;

[Index("nickname", Name = "UQ__user_pro__5CF1C59BC90D0082", IsUnique = true)]
[Index("user_id", Name = "UQ__user_pro__B9BE370E12C981DE", IsUnique = true)]
public partial class user_profile
{
    [Key]
    public int id { get; set; }

    public int user_id { get; set; }

    [StringLength(100)]
    [Unicode(false)]
    public string? nickname { get; set; }

    public string? avatar_url { get; set; }

    public string? bio { get; set; }

    public string? description { get; set; }

    public string? social_links { get; set; }

    public string? settings { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? updated_at { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("user_profile")]
    public virtual user user { get; set; } = null!;
}
