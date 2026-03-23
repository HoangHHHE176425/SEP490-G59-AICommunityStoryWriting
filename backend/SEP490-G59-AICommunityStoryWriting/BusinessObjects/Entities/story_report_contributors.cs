using System;

namespace BusinessObjects.Entities;

/// <summary>Mỗi user chỉ một dòng / truyện (toàn thời gian): audit + chống báo cáo trùng.</summary>
public partial class story_report_contributors
{
    public Guid story_id { get; set; }

    public Guid user_id { get; set; }

    public string reason_category { get; set; } = null!;

    public string? description { get; set; }

    public DateTime created_at { get; set; }
}
