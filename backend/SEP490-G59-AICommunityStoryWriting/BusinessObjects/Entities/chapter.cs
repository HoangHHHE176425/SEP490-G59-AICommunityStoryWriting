using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Chapter
{
    public Guid Id { get; set; }

    public Guid? StoryId { get; set; }

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

    public virtual ICollection<AiGeneratedContent> AiGeneratedContents { get; set; } = new List<AiGeneratedContent>();

    public virtual ICollection<AiPlagiarismReport> AiPlagiarismReports { get; set; } = new List<AiPlagiarismReport>();

    public virtual ICollection<ChapterVersion> ChapterVersions { get; set; } = new List<ChapterVersion>();

    public virtual Story? Story { get; set; }
}
