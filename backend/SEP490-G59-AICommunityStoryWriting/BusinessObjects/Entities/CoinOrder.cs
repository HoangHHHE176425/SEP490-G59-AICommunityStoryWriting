using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class CoinOrder
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid PackageId { get; set; }

    public decimal AmountPaid { get; set; }

    public int CoinsGranted { get; set; }

    public string? PaymentGateway { get; set; }

    public string? GatewayTransactionId { get; set; }

    public string? GatewayResponseCode { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public virtual CoinPackage Package { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
