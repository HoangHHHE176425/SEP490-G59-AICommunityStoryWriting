using System.Text.Json.Serialization;

namespace Services.DTOs.Admin;

/// <summary>Hạn mức token AI mặc định sẽ set tự động khi user lần đầu trở thành AUTHOR. Null = không set cột đó.</summary>
public sealed class AuthorAiTokenDefaultsOnBecomeAuthorDto
{
    [JsonPropertyName("lifetime")]
    public long? Lifetime { get; init; }

    [JsonPropertyName("perDay")]
    public long? PerDay { get; init; }

    [JsonPropertyName("perWeek")]
    public long? PerWeek { get; init; }

    [JsonPropertyName("perMonth")]
    public long? PerMonth { get; init; }
}

