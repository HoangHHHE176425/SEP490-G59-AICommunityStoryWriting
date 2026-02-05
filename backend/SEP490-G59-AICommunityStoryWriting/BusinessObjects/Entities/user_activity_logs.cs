using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class user_activity_logs
{
    public long id { get; set; }

    public Guid? user_id { get; set; }

    public string? action_type { get; set; }

    public string? description { get; set; }

    public string? raw_data { get; set; }

    public string? ip_address { get; set; }

    public string? device_info { get; set; }

    public DateTime? created_at { get; set; }

    public virtual users? user { get; set; }
}
