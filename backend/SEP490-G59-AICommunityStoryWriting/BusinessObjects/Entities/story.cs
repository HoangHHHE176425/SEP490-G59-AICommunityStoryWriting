using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Story
{
    public Guid Id { get; set; }

    public Guid? AuthorId { get; set; }

    public string Title { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? CoverImage { get; set; }

    public string? Summary { get; set; }

    public string? Status { get; set; }

    public string StoryProgressStatus { get; set; } = null!;

    public DateTime? LastPublishedAt { get; set; }

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

    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<IdeaPost> IdeaPosts { get; set; } = new List<IdeaPost>();

    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual ICollection<StoryCommitment> StoryCommitments { get; set; } = new List<StoryCommitment>();

    public virtual ICollection<UserLibrary> UserLibraries { get; set; } = new List<UserLibrary>();

    public virtual ICollection<Category> Categories { get; set; } = new List<Category>();
}
