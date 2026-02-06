using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class DailyStatistic
{
    public DateOnly StatDate { get; set; }

    public int? NewUsersCount { get; set; }

    public int? ActiveUsersCount { get; set; }

    public int? NewStoriesCount { get; set; }

    public int? NewChaptersCount { get; set; }

    public long? TotalViewsDay { get; set; }

    public decimal? TotalRevenueNaira { get; set; }

    public decimal? TotalWithdrawalsPaid { get; set; }

    public int? TotalCoinsSpent { get; set; }

    public int? PendingReportsCount { get; set; }

    public int? PendingWithdrawalsCount { get; set; }

    public DateTime? UpdatedAt { get; set; }
}
