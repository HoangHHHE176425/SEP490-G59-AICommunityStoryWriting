using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class coin_packages
{
    public int id { get; set; }

    public string name { get; set; } = null!;

    public decimal price_amount { get; set; }

    public string? currency { get; set; }

    public int coin_amount { get; set; }

    public int? bonus_coin { get; set; }

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }

    public virtual ICollection<coin_orders> coin_orders { get; set; } = new List<coin_orders>();
}
