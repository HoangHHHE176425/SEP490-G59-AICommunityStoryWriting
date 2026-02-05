using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class notifications
{
    public Guid id { get; set; }

    public Guid? user_id { get; set; }

    public string? type { get; set; }

    public string? title { get; set; }

    public string? content { get; set; }

    public string? link_url { get; set; }

    public bool? is_read { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? user { get; set; }
}
