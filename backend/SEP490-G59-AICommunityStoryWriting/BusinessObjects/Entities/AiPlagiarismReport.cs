using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AiPlagiarismReport
{
    public Guid Id { get; set; }

    public Guid? ChapterId { get; set; }

    public decimal? SimilarityRatio { get; set; }

    public string? SourceDetails { get; set; }

    public string? Status { get; set; }

    public DateTime? CheckedAt { get; set; }

    public virtual Chapter? Chapter { get; set; }
}
