using Repositories;

namespace AIStory.API.Services;

/// <summary>Đọc/ghi giới hạn AI từ bảng ai_configs (hai key tách biệt); fallback config + key cũ.</summary>
public class AIUsageLimitConfigService : IAIUsageLimitConfigService
{
    /// <summary>Key legacy (áp dụng cho cả hai loại nếu key mới chưa có).</summary>
    public const string LegacySettingKey = "RateLimitMaxRequestsPerDay";

    public const string SettingKeySuggest = "RateLimitMaxRequestsPerDaySuggestNextChapter";
    public const string SettingKeyCoCreate = "RateLimitMaxRequestsPerDayCoCreate";

    private const int DefaultMax = 3;
    private const int MinValue = 1;
    private const int MaxValue = 100;

    private readonly IAiConfigsRepository _aiConfigs;
    private readonly IConfiguration _configuration;

    public AIUsageLimitConfigService(IAiConfigsRepository aiConfigs, IConfiguration configuration)
    {
        _aiConfigs = aiConfigs;
        _configuration = configuration;
    }

    public int GetMaxRequestsPerDay(AiRateLimitKind kind)
    {
        var specificKey = kind == AiRateLimitKind.SuggestNextChapter ? SettingKeySuggest : SettingKeyCoCreate;
        var fromDbSpecific = _aiConfigs.GetValue(specificKey);
        if (!string.IsNullOrWhiteSpace(fromDbSpecific) && int.TryParse(fromDbSpecific.Trim(), out var ns) && ns >= MinValue && ns <= MaxValue)
            return ns;

        var fromConfigSpecific = kind == AiRateLimitKind.SuggestNextChapter
            ? _configuration.GetValue<int?>("AI:RateLimitSuggestNextChapterPerDay")
            : _configuration.GetValue<int?>("AI:RateLimitCoCreatePerDay");
        if (fromConfigSpecific.HasValue && fromConfigSpecific.Value >= MinValue) return fromConfigSpecific.Value;

        // Legacy: một key chung trong DB / appsettings
        var fromDbLegacy = _aiConfigs.GetValue(LegacySettingKey);
        if (!string.IsNullOrWhiteSpace(fromDbLegacy) && int.TryParse(fromDbLegacy.Trim(), out var nl) && nl >= MinValue && nl <= MaxValue)
            return nl;
        var fromConfigLegacy = _configuration.GetValue<int?>("AI:RateLimitMaxRequestsPerDay");
        if (fromConfigLegacy.HasValue && fromConfigLegacy.Value >= MinValue) return fromConfigLegacy.Value;

        return DefaultMax;
    }

    public void SetMaxRequestsPerDay(AiRateLimitKind kind, int value, Guid? updatedBy = null)
    {
        var clamped = Math.Clamp(value, MinValue, MaxValue);
        var key = kind == AiRateLimitKind.SuggestNextChapter ? SettingKeySuggest : SettingKeyCoCreate;
        var desc = kind == AiRateLimitKind.SuggestNextChapter
            ? "Số lần tối đa POST suggest-next-chapter trong 24h (rolling)."
            : "Số lần tối đa POST co-create trong 24h (rolling).";
        _aiConfigs.Upsert(key, clamped.ToString(), desc);
    }
}
