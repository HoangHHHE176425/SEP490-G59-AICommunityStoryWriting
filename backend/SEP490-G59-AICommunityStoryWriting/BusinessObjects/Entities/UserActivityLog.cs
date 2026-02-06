using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class UserActivityLog
{
    public long Id { get; set; }

    public Guid? UserId { get; set; }

    public string? ActionType { get; set; }

    public string? Description { get; set; }

    public string? RawData { get; set; }

    public string? IpAddress { get; set; }

    public string? DeviceInfo { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
