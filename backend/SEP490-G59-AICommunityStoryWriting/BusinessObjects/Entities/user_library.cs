using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class user_library
{
    public Guid user_id { get; set; }

    public Guid story_id { get; set; }

    public string relation_type { get; set; } = null!;

    public Guid? last_read_chapter_id { get; set; }

    public DateTime? last_read_at { get; set; }

    public virtual stories story { get; set; } = null!;

    public virtual users user { get; set; } = null!;
}
