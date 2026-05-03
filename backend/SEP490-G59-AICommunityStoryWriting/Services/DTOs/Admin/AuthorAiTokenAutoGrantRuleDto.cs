using System.Text.Json.Serialization;

namespace Services.DTOs.Admin;

public sealed class AuthorAiTokenAutoGrantRuleDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; init; }

    [JsonPropertyName("isEnabled")]
    public bool IsEnabled { get; init; }

    [JsonPropertyName("displayName")]
    public string? DisplayName { get; init; }

    [JsonPropertyName("periodKind")]
    public string PeriodKind { get; init; } = null!;

    [JsonPropertyName("grantLimitField")]
    public string GrantLimitField { get; init; } = null!;

    [JsonPropertyName("grantAmount")]
    public long GrantAmount { get; init; }

    [JsonPropertyName("applyToAllAuthors")]
    public bool ApplyToAllAuthors { get; init; }

    [JsonPropertyName("selectedUserIds")]
    public IReadOnlyList<Guid> SelectedUserIds { get; init; } = Array.Empty<Guid>();

    [JsonPropertyName("lastExecutedPeriodKey")]
    public string? LastExecutedPeriodKey { get; init; }

    [JsonPropertyName("lastRunAtUtc")]
    public DateTime? LastRunAtUtc { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime CreatedAtUtc { get; init; }

    [JsonPropertyName("updatedAtUtc")]
    public DateTime UpdatedAtUtc { get; init; }
}
