using System;
using System.Collections.Generic;

namespace StoryPlatform.Models;

public partial class Chapter
{
    public string Id { get; set; } = null!;

    public string StoryId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int OrderIndex { get; set; }

    public string? Content { get; set; }

    public string? Status { get; set; }

    public string? AccessType { get; set; }

    public int? CoinPrice { get; set; }

    public int? WordCount { get; set; }

    public decimal? AiContributionRatio { get; set; }

    public bool? IsAiClean { get; set; }

    public DateTime? PublishedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual Story Story { get; set; } = null!;
}
