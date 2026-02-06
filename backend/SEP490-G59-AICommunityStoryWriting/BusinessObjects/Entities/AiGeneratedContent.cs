using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AiGeneratedContent
{
    public Guid Id { get; set; }

    public Guid? ChapterId { get; set; }

    public Guid? UserId { get; set; }

    public string? InputPrompt { get; set; }

    public string? AiOutput { get; set; }

    public decimal? SimilarityScore { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual Chapter? Chapter { get; set; }
}
