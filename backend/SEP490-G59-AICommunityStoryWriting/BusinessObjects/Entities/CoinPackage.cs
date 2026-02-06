using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class CoinPackage
{
    public Guid Id { get; set; }

    public string Name { get; set; } = null!;

    public decimal PriceAmount { get; set; }

    public string? Currency { get; set; }

    public int CoinAmount { get; set; }

    public int? BonusCoin { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<CoinOrder> CoinOrders { get; set; } = new List<CoinOrder>();
}
