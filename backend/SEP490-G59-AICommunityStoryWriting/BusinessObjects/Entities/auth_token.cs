using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Entities;

[Index("refresh_token", Name = "UQ__auth_tok__7FB69BAD2FA55EAC", IsUnique = true)]
public partial class auth_token
{
    [Key]
    public int id { get; set; }

    public int user_id { get; set; }

    [StringLength(255)]
    [Unicode(false)]
    public string refresh_token { get; set; } = null!;

    public string? device_info { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime expires_at { get; set; }

    [Column(TypeName = "datetime")]
    public DateTime? created_at { get; set; }

    [ForeignKey("user_id")]
    [InverseProperty("auth_tokens")]
    public virtual user user { get; set; } = null!;
}
