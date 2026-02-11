using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class author_policy_acceptances
{
    public Guid id { get; set; }

    public Guid user_id { get; set; }

    public Guid policy_id { get; set; }

    public DateTime accepted_at { get; set; }

    public string? ip_address { get; set; }

    public string? user_agent { get; set; }

    public string accepted_for { get; set; } = null!;

    public virtual users user { get; set; } = null!;

    public virtual system_policies policy { get; set; } = null!;
}

