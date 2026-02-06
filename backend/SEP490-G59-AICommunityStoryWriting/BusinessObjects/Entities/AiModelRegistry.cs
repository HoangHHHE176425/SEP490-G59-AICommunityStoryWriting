using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AiModelRegistry
{
    public Guid Id { get; set; }

    public string? Provider { get; set; }

    public string? ModelName { get; set; }

    public bool? IsDefaultForGen { get; set; }

    public bool? IsDefaultForMod { get; set; }

    public string? ApiKeyVaultRef { get; set; }

    public int? MaxTokens { get; set; }

    public decimal? Temperature { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
