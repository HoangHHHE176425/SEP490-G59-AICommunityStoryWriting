using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class withdraw_requests
{
    public Guid id { get; set; }

    public Guid? author_id { get; set; }

    public decimal amount_requested { get; set; }

    public decimal? fee_amount { get; set; }

    public string bank_info_snapshot { get; set; } = null!;

    public string? status { get; set; }

    public string? admin_note { get; set; }

    public string? transaction_proof_url { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? processed_at { get; set; }

    public Guid? processed_by { get; set; }

    public virtual users? author { get; set; }

    public virtual users? processed_byNavigation { get; set; }
}
