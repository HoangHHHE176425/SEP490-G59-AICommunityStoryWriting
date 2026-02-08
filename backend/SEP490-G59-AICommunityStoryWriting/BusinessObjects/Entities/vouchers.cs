using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class vouchers
{
    public Guid id { get; set; }

    public string code { get; set; } = null!;

    public string? type { get; set; }

    public decimal? value { get; set; }

    public int? min_order_value { get; set; }

    public decimal? max_discount_amount { get; set; }

    public int? usage_limit { get; set; }

    public int? used_count { get; set; }

    public DateTime? start_date { get; set; }

    public DateTime? expiry_date { get; set; }

    public bool? is_active { get; set; }

    public DateTime? created_at { get; set; }

    public virtual ICollection<user_vouchers> user_vouchers { get; set; } = new List<user_vouchers>();
}
