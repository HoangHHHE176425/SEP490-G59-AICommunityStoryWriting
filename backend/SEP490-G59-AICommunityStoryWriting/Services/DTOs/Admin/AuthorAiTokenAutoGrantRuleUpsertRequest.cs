using System.Text.Json.Serialization;

namespace Services.DTOs.Admin;

public sealed class AuthorAiTokenAutoGrantRuleUpsertRequest
{
    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; set; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; set; }

    /// <summary>daily_utc | weekly_utc | monthly_utc</summary>
    [JsonPropertyName("periodKind")]
    public string PeriodKind { get; set; } = null!;

    /// <summary>lifetime | per_day | per_week | per_month</summary>
    [JsonPropertyName("grantLimitField")]
    public string GrantLimitField { get; set; } = null!;

    [JsonPropertyName("grantAmount")]
    public long GrantAmount { get; set; }

    [JsonPropertyName("applyToAllAuthors")]
    public bool ApplyToAllAuthors { get; set; }

    [JsonPropertyName("selectedUserIds")]
    public List<Guid>? SelectedUserIds { get; set; }
}
