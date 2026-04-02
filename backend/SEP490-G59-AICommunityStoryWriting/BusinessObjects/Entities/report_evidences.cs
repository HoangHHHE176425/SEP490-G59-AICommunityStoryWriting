using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class report_evidences
{
    public Guid id { get; set; }

    public Guid? report_id { get; set; }

    public string? evidence_url { get; set; }

    public string? evidence_text { get; set; }

    /// <summary>COMPLIANCE đã đánh dấu bằng chứng báo cáo (request của người report) là đã xác minh.</summary>
    public DateTime? compliance_verified_at_utc { get; set; }

    public Guid? compliance_verified_by_user_id { get; set; }

    public virtual reports? report { get; set; }
}
