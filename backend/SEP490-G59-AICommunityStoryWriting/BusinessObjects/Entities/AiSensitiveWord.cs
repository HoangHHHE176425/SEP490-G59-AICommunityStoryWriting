using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AiSensitiveWord
{
    public Guid Id { get; set; }

    public string Word { get; set; } = null!;

    public string? Category { get; set; }

    public DateTime? CreatedAt { get; set; }
}
