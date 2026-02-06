using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Rating
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? StoryId { get; set; }

    public int? StarValue { get; set; }

    public string? ReviewText { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Story? Story { get; set; }

    public virtual User? User { get; set; }
}
