using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

/// <summary>Story Memory Engine: snapshot trạng thái truyện (thế giới, quy tắc, điểm đến). Một bản ghi per story, cập nhật khi có chương mới.</summary>
public partial class story_story_state
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    /// <summary>JSON: world rules, current story state, locations, v.v.</summary>
    public string? state_snapshot_json { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual stories story { get; set; } = null!;
}
