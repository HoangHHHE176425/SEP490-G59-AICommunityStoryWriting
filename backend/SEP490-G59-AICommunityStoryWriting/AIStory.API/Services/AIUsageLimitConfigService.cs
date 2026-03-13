using Repositories;

namespace AIStory.API.Services;

/// <summary>Đọc/ghi giới hạn AI từ bảng ai_configs (key RateLimitMaxRequestsPerDay); fallback config.</summary>
public class AIUsageLimitConfigService : IAIUsageLimitConfigService
{
    /// <summary>Key trong ai_configs (max 50 ký tự).</summary>
    public const string SettingKey = "RateLimitMaxRequestsPerDay";
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

    public int GetMaxRequestsPerDay()
    {
        var fromDb = _aiConfigs.GetValue(SettingKey);
        if (!string.IsNullOrWhiteSpace(fromDb) && int.TryParse(fromDb.Trim(), out var n) && n >= MinValue && n <= MaxValue)
            return n;
        var fromConfig = _configuration.GetValue<int?>("AI:RateLimitMaxRequestsPerDay");
        if (fromConfig.HasValue && fromConfig.Value >= MinValue) return fromConfig.Value;
        return DefaultMax;
    }

    public void SetMaxRequestsPerDay(int value, Guid? updatedBy = null)
    {
        var clamped = Math.Clamp(value, MinValue, MaxValue);
        _aiConfigs.Upsert(SettingKey, clamped.ToString(), "Số lần tối đa sử dụng AI trong 24h (rolling).");
    }
}
