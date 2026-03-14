using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class story_character_memory
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    public string character_name { get; set; } = null!;

    public string? state_json { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual stories story { get; set; } = null!;
}
