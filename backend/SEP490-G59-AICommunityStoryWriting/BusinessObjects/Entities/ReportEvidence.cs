using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ReportEvidence
{
    public Guid Id { get; set; }

    public Guid? ReportId { get; set; }

    public string? EvidenceUrl { get; set; }

    public string? EvidenceText { get; set; }

    public virtual Report? Report { get; set; }
}
