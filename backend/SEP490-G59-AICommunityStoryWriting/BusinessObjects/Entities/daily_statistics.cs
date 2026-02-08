using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class daily_statistics
{
    public DateOnly stat_date { get; set; }

    public int? new_users_count { get; set; }

    public int? active_users_count { get; set; }

    public int? new_stories_count { get; set; }

    public int? new_chapters_count { get; set; }

    public long? total_views_day { get; set; }

    public decimal? total_revenue_naira { get; set; }

    public decimal? total_withdrawals_paid { get; set; }

    public int? total_coins_spent { get; set; }

    public int? pending_reports_count { get; set; }

    public int? pending_withdrawals_count { get; set; }

    public DateTime? updated_at { get; set; }
}
