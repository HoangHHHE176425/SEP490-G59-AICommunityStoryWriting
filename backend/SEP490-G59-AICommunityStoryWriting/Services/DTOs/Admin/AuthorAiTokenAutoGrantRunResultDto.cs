using System.Text.Json.Serialization;

namespace Services.DTOs.Admin;

public sealed class AuthorAiTokenAutoGrantRunResultDto
{
    [JsonPropertyName("ruleId")]
    public Guid RuleId { get; init; }

    [JsonPropertyName("periodKey")]
    public string PeriodKey { get; init; } = null!;

    [JsonPropertyName("usersUpdated")]
    public int UsersUpdated { get; init; }
}
