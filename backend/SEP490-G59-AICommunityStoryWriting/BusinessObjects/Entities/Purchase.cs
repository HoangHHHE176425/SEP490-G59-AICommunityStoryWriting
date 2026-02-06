using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Purchase
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? StoryId { get; set; }

    public Guid? ChapterId { get; set; }

    public int PricePaid { get; set; }

    public string? PurchaseType { get; set; }

    public string? EscrowStatus { get; set; }

    public DateTime? ReleasedAt { get; set; }

    public decimal? PlatformFeeRatio { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Story? Story { get; set; }

    public virtual User? User { get; set; }
}
