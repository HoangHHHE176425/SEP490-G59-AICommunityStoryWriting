using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ai_model_registry
{
    public Guid id { get; set; }

    public string? provider { get; set; }

    public string? model_name { get; set; }

    public bool? is_default_for_gen { get; set; }

    public bool? is_default_for_mod { get; set; }

    public string? api_key_vault_ref { get; set; }

    public int? max_tokens { get; set; }

    public decimal? temperature { get; set; }

    public bool? is_active { get; set; }

    public DateTime? updated_at { get; set; }
}
