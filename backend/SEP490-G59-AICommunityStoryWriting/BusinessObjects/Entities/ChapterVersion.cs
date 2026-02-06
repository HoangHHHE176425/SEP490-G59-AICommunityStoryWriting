using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ChapterVersion
{
    public Guid Id { get; set; }

    public Guid? ChapterId { get; set; }

    public Guid? AuthorId { get; set; }

    public string? ContentSnapshot { get; set; }

    public int VersionNumber { get; set; }

    public string? ChangeSummary { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual User? Author { get; set; }

    public virtual Chapter? Chapter { get; set; }
}
