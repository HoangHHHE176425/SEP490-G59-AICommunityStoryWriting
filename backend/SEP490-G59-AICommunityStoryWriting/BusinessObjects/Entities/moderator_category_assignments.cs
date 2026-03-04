using System;

namespace BusinessObjects.Entities;

public partial class moderator_category_assignments
{
    public Guid moderator_id { get; set; }
    public Guid category_id { get; set; }
    public DateTime? assigned_at { get; set; }

    public virtual users moderator { get; set; } = null!;
    public virtual categories category { get; set; } = null!;
}

