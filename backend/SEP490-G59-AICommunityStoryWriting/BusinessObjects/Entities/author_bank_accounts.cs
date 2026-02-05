using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class author_bank_accounts
{
    public Guid user_id { get; set; }

    public string bank_name { get; set; } = null!;

    public string account_number { get; set; } = null!;

    public string account_holder_name { get; set; } = null!;

    public string? branch_name { get; set; }

    public bool? is_verified { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual users user { get; set; } = null!;
}
