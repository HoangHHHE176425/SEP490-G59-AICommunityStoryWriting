using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class AiUsageLog
{
    public long Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? StoryId { get; set; }

    public Guid? ChapterId { get; set; }

    public string? ActionType { get; set; }

    public string? ModelName { get; set; }

    public int? PromptTokens { get; set; }

    public int? CompletionTokens { get; set; }

    public int? TotalTokens { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? User { get; set; }
}
