using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class wallets
{
    public Guid user_id { get; set; }

    public int? balance_coin { get; set; }

    public decimal? income_balance { get; set; }

    public decimal? frozen_balance { get; set; }

    public string? currency { get; set; }

    public decimal? pending_escrow_balance { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual users user { get; set; } = null!;
}
