using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class MarketingBanner
{
    public Guid Id { get; set; }

    public string? Title { get; set; }

    public string ImageUrl { get; set; } = null!;

    public string? LinkUrl { get; set; }

    public string? Position { get; set; }

    public int? Priority { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }
}
