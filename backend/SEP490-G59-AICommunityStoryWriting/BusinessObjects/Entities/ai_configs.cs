using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ai_configs
{
    public string key { get; set; } = null!;

    public string? value { get; set; }

    public string? description { get; set; }

    public DateTime? updated_at { get; set; }
}
