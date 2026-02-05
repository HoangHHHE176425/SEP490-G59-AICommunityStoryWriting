using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class user_vouchers
{
    public Guid user_id { get; set; }

    public Guid voucher_id { get; set; }

    public DateTime? applied_at { get; set; }

    public Guid? order_id { get; set; }

    public virtual users user { get; set; } = null!;

    public virtual vouchers voucher { get; set; } = null!;
}
