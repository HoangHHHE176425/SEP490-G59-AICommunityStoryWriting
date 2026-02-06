using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AiConfig
{
    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public string? Description { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
