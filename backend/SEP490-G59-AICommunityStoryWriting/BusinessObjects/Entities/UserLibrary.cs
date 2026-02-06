using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class UserLibrary
{
    public Guid UserId { get; set; }

    public Guid StoryId { get; set; }

    public string RelationType { get; set; } = null!;

    public Guid? LastReadChapterId { get; set; }

    public DateTime? LastReadAt { get; set; }

    public virtual Story Story { get; set; } = null!;

    public virtual User User { get; set; } = null!;
}
