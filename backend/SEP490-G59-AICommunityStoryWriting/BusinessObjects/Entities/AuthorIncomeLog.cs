using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AuthorIncomeLog
{
    public long Id { get; set; }

    public Guid? AuthorId { get; set; }

    public string? SourceType { get; set; }

    public Guid? SourceId { get; set; }

    public decimal? GrossAmount { get; set; }

    public decimal? PlatformFee { get; set; }

    public decimal? NetAmount { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? Author { get; set; }
}
