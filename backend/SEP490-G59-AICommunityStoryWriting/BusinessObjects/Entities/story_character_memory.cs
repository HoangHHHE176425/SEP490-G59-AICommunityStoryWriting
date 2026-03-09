using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

/// <summary>Story Memory Engine: trạng thái / tính cách nhân vật theo truyện. Plot Manager cập nhật khi có chương mới.</summary>
public partial class story_character_memory
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    /// <summary>Tên hoặc định danh nhân vật.</summary>
    public string character_name { get; set; } = null!;

    /// <summary>JSON: personality, relationships, current state (vd. đã chết, đang ở đâu, đồ vật đang có).</summary>
    public string? state_json { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual stories story { get; set; } = null!;
}
