using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class author_income_logs
{
    public long id { get; set; }

    public Guid? author_id { get; set; }

    public string? source_type { get; set; }

    public Guid? source_id { get; set; }

    public decimal? gross_amount { get; set; }

    public decimal? platform_fee { get; set; }

    public decimal? net_amount { get; set; }

    public string? status { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? author { get; set; }
}
