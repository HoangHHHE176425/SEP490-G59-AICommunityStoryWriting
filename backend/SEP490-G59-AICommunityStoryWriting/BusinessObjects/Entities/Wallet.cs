using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Wallet
{
    public Guid UserId { get; set; }

    public int? BalanceCoin { get; set; }

    public decimal? IncomeBalance { get; set; }

    public decimal? FrozenBalance { get; set; }

    public string? Currency { get; set; }

    public decimal? PendingEscrowBalance { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual User User { get; set; } = null!;
}
