using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class report_evidences
{
    public int id { get; set; }

    public Guid? report_id { get; set; }

    public string? evidence_url { get; set; }

    public string? evidence_text { get; set; }

    public virtual reports? report { get; set; }
}
