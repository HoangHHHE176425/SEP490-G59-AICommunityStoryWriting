using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class SystemPolicy
{
    public Guid Id { get; set; }

    public string? Type { get; set; }

    public string Version { get; set; } = null!;

    public string Content { get; set; } = null!;

    public bool? IsActive { get; set; }

    public bool? RequireResign { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ActivatedAt { get; set; }
}
