namespace Services.DTOs.Admin;

/// <summary>Dữ liệu từ GET https://openrouter.ai/api/v1/key (trường data.*).</summary>
public sealed class OpenRouterKeyStatsDto
{
    public double Usage { get; set; }
    public double UsageDaily { get; set; }
    public double UsageWeekly { get; set; }
    public double UsageMonthly { get; set; }
    public double? Limit { get; set; }
    public double? LimitRemaining { get; set; }
    public string? LimitReset { get; set; }
    public string? Label { get; set; }
    public bool IsFreeTier { get; set; }
}

/// <summary>Một dòng trong GET https://openrouter.ai/api/v1/keys (usage theo từng API key tiêu phí model).</summary>
/// <summary>Dữ liệu từ GET https://openrouter.ai/api/v1/credits (account, cần Management key).</summary>
public sealed class OpenRouterCreditsDto
{
    /// <summary>Tổng credits đã nạp / mua (USD).</summary>
    public double? TotalCreditsPurchased { get; set; }

    /// <summary>Tổng credits đã tiêu (USD).</summary>
    public double? TotalCreditsUsed { get; set; }

    /// <summary>Còn lại nếu API trả về; nếu không thì client có thể tính từ nạp − dùng.</summary>
    public double? RemainingCredits { get; set; }
}

public sealed class OpenRouterKeyListItemDto
{
    public string? Hash { get; set; }
    public string? Label { get; set; }
    public string? Name { get; set; }
    public double Usage { get; set; }
    public double UsageDaily { get; set; }
    public double UsageWeekly { get; set; }
    public double UsageMonthly { get; set; }
    public double? Limit { get; set; }
    public double? LimitRemaining { get; set; }
    public string? LimitReset { get; set; }
    public bool? Disabled { get; set; }
}

public sealed class AdminAiRequestLogItemDto
{
    public long Id { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public Guid? UserId { get; set; }
    public string? UserEmail { get; set; }
    public Guid? StoryId { get; set; }
    public Guid? ChapterId { get; set; }
    public string? ActionType { get; set; }
    public string? ModelName { get; set; }
    public string? GenerationId { get; set; }
    public decimal? CostUsd { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    public int? TotalTokens { get; set; }
    public string? Status { get; set; }
}

public sealed class AdminAiRequestLogsPageDto
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public IReadOnlyList<AdminAiRequestLogItemDto> Items { get; set; } = Array.Empty<AdminAiRequestLogItemDto>();
}

/// <summary>Số request AI theo từng ngày (UTC date) — dùng cho biểu đồ Generations.</summary>
public sealed class AdminAiGenerationDayCountDto
{
    public string Day { get; set; } = "";
    public int Count { get; set; }
}

public sealed class AdminAiGenerationsDailyResponseDto
{
    public IReadOnlyList<AdminAiGenerationDayCountDto> Days { get; set; } = Array.Empty<AdminAiGenerationDayCountDto>();
}
