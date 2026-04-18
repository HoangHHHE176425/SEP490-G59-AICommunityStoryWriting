using Services.DTOs.Admin;

namespace Services.Interfaces;

public interface IAdminAiUsageStatsService
{
    /// <summary>True nếu cấu hình AI dùng OpenRouter (BaseUrl chứa openrouter.ai).</summary>
    bool IsOpenRouterConfigured();

    Task<(bool Ok, string? Error, OpenRouterKeyStatsDto? Data)> GetOpenRouterKeyStatsAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/keys — cần Management key; trả usage/limit theo từng API key.</summary>
    Task<(bool Ok, string? Error, IReadOnlyList<OpenRouterKeyListItemDto>? Keys)> GetOpenRouterKeysListAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/keys/{{hash}} — cần Management key.</summary>
    Task<(bool Ok, string? Error, string? Json)> GetOpenRouterKeyByHashRawJsonAsync(string hash, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/activity — analytics account-level (30 ngày UTC gần nhất); cần Management key.</summary>
    Task<(bool Ok, string? Error, string? Json)> GetOpenRouterActivityRawJsonAsync(
        string? date,
        string? apiKeyHash,
        string? userId,
        CancellationToken cancellationToken = default);

    /// <summary>GET /api/v1/credits — tổng đã nạp / đã dùng (USD); cần Management key.</summary>
    Task<(bool Ok, string? Error, OpenRouterCreditsDto? Data)> GetOpenRouterCreditsAsync(
        CancellationToken cancellationToken = default);

    Task<AdminAiRequestLogsPageDto> GetRequestLogsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? actionType,
        string? modelName,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>Đếm số dòng ai_usage_logs theo ngày UTC (cùng bộ lọc với log phân trang).</summary>
    Task<AdminAiGenerationsDailyResponseDto> GetGenerationsDailyCountsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? modelName,
        string? status,
        string? actionType,
        CancellationToken cancellationToken = default);

    Task<(bool Ok, string? Error, string? Json)> GetOpenRouterGenerationRawJsonAsync(string generationId, CancellationToken cancellationToken = default);
}
