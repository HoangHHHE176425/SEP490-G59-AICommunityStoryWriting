using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Comment
{
    public Guid Id { get; set; }

    public Guid? UserId { get; set; }

    public Guid? StoryId { get; set; }

    public Guid? ChapterId { get; set; }

    public Guid? ParentId { get; set; }

    public string Content { get; set; } = null!;

    public int? LikesCount { get; set; }

    public string? Status { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<Comment> InverseParent { get; set; } = new List<Comment>();

    public virtual Comment? Parent { get; set; }

    public virtual Story? Story { get; set; }

    public virtual User? User { get; set; }

    public virtual ICollection<User> Users { get; set; } = new List<User>();
}
