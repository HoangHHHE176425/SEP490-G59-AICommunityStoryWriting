using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

/// <summary>Story Memory Engine: sự kiện trong timeline truyện. Plot Manager thêm mới khi có chương.</summary>
public partial class story_event_memory
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    /// <summary>Chương mà sự kiện xảy ra (nullable nếu chưa gắn chương).</summary>
    public Guid? chapter_id { get; set; }

    /// <summary>Thứ tự trong timeline (0, 1, 2...).</summary>
    public int order_index { get; set; }

    /// <summary>Mô tả ngắn sự kiện (vd. "Sư phụ chết tại chương 2").</summary>
    public string description { get; set; } = null!;

    public DateTime? created_at { get; set; }

    public virtual chapters? chapter { get; set; }

    public virtual stories story { get; set; } = null!;
}
