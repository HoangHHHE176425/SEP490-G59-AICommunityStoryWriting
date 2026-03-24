using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class story_story_state
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    public string? state_snapshot_json { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual stories story { get; set; } = null!;
}
