using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class UserVoucher
{
    public Guid UserId { get; set; }

    public Guid VoucherId { get; set; }

    public DateTime? AppliedAt { get; set; }

    public Guid? OrderId { get; set; }

    public virtual User User { get; set; } = null!;

    public virtual Voucher Voucher { get; set; } = null!;
}
