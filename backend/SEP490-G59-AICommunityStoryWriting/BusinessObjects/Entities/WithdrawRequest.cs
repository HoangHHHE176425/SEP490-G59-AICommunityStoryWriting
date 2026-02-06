using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class WithdrawRequest
{
    public Guid Id { get; set; }

    public Guid? AuthorId { get; set; }

    public decimal AmountRequested { get; set; }

    public decimal? FeeAmount { get; set; }

    public string BankInfoSnapshot { get; set; } = null!;

    public string? Status { get; set; }

    public string? AdminNote { get; set; }

    public string? TransactionProofUrl { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? ProcessedAt { get; set; }

    public Guid? ProcessedBy { get; set; }

    public virtual User? Author { get; set; }

    public virtual User? ProcessedByNavigation { get; set; }
}
