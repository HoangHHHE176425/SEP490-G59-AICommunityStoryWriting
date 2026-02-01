using System;
using System.Collections.Generic;

namespace StoryPlatform.Models;

public partial class Story
{
    public string Id { get; set; } = null!;

    public string? AuthorId { get; set; }

    public int? CategoryId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? CoverImage { get; set; }

    public string? Summary { get; set; }

    public string? Status { get; set; }

    public string? SaleType { get; set; }

    public int? FullStoryPrice { get; set; }

    public int? ExpectedChapters { get; set; }

    public int? ReleaseFrequencyDays { get; set; }

    public DateTime? LastPublishedAt { get; set; }

    public string? AccessType { get; set; }

    public int? TotalChapters { get; set; }

    public long? TotalViews { get; set; }

    public int? TotalFavorites { get; set; }

    public decimal? AvgRating { get; set; }

    public int? WordCount { get; set; }

    public string? AgeRating { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public DateTime? PublishedAt { get; set; }

    public virtual User? Author { get; set; }

    public virtual Category? Category { get; set; }

    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();
}
