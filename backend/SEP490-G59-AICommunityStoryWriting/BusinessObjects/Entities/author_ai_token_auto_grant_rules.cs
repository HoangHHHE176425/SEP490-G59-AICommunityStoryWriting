using System;

namespace BusinessObjects.Entities;

/// <summary>Quy tắc gia hạn token AI tự động: admin chọn phạm vi tài khoản, số token cộng thêm và chu kỳ (ngày/tuần/tháng UTC).</summary>
public partial class author_ai_token_auto_grant_rules
{
    public Guid id { get; set; }

    public bool is_enabled { get; set; }

    public string? display_name { get; set; }

    /// <summary>daily_utc | weekly_utc | monthly_utc</summary>
    public string period_kind { get; set; } = null!;

    /// <summary>lifetime | per_day | per_week | per_month — cột users được cộng thêm <see cref="grant_amount"/>.</summary>
    public string grant_limit_field { get; set; } = null!;

    public long grant_amount { get; set; }

    public bool apply_to_all_authors { get; set; }

    /// <summary>JSON mảng GUID (string), dùng khi <see cref="apply_to_all_authors"/> = false.</summary>
    public string? selected_user_ids { get; set; }

    public string? last_executed_period_key { get; set; }

    public DateTime? last_run_at_utc { get; set; }

    public DateTime created_at_utc { get; set; }

    public DateTime updated_at_utc { get; set; }
}
