using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class Category
{
    public int Id { get; set; }

    public string Name { get; set; } = null!;

    public string Slug { get; set; } = null!;

    public string? Description { get; set; }

    public string? IconUrl { get; set; }

    public bool? IsActive { get; set; }

    public DateTime? CreatedAt { get; set; }

    public virtual ICollection<Story> Stories { get; set; } = new List<Story>();
}
