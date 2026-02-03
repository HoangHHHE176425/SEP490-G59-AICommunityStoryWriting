using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class system_settings
{
    public string key { get; set; } = null!;

    public string value { get; set; } = null!;

    public string? value_type { get; set; }

    public string? description { get; set; }

    public DateTime? updated_at { get; set; }

    public Guid? updated_by { get; set; }

    public virtual users? updated_byNavigation { get; set; }
}
