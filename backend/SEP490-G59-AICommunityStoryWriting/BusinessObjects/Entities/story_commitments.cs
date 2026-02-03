using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class story_commitments
{
    public Guid id { get; set; }

    public Guid? story_id { get; set; }

    public Guid? user_id { get; set; }

    public string? policy_version { get; set; }

    public DateTime? signed_at { get; set; }

    public string? ip_address { get; set; }

    public virtual stories? story { get; set; }

    public virtual users? user { get; set; }
}
