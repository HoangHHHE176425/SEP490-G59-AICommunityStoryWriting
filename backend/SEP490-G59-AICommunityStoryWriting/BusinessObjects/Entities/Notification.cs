using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Notification
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public string? Type { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? LinkUrl { get; set; }

    public bool? IsRead { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
