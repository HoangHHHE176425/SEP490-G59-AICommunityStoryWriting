using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class ai_sensitive_words
{
    public int id { get; set; }

    public string word { get; set; } = null!;

    public string? category { get; set; }

    public DateTime? created_at { get; set; }
}
