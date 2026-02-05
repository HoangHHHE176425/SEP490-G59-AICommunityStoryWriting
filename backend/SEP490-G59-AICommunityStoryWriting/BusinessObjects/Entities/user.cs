using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Entities;

[Index("email", Name = "UQ__users__AB6E61643F214041", IsUnique = true)]
public partial class user
{
    [Key]
    public int id { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string email { get; set; } = null!;

    public string password_hash { get; set; } = null!;

    [StringLength(20)]
    [Unicode(false)]
    public string? role { get; set; }

    [StringLength(20)]
    [Unicode(false)]
    public string? status { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? email_verified_at { get; set; }

    public bool? must_resign_policy { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? deletion_requested_at { get; set; }

    public bool? is_author { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? created_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? updated_at { get; set; }

    [InverseProperty("user")]
    public virtual ICollection<auth_token> auth_tokens { get; set; } = new List<auth_token>();

    [InverseProperty("author")]
    public virtual ICollection<story> stories { get; set; } = new List<story>();

    [InverseProperty("user")]
    public virtual user_profile? user_profile { get; set; }
}
