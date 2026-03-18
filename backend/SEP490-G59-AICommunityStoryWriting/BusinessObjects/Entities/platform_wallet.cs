using System;

namespace BusinessObjects.Entities;

public partial class platform_wallet
{
    public int id { get; set; }

    public int balance_coin { get; set; }

    public DateTime? updated_at { get; set; }
}

