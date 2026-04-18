using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace AIStory.API.Controllers;

/// <summary>Thống kê AI cho admin: OpenRouter (usage/limit) + log từng request trong DB + chi tiết generation OpenRouter.</summary>
[ApiController]
[Route("api/admin/ai-usage-stats")]
[Authorize(Roles = "ADMIN")]
public class AdminAiUsageStatsController : ControllerBase
{
    private readonly IAdminAiUsageStatsService _stats;

    public AdminAiUsageStatsController(IAdminAiUsageStatsService stats)
    {
        _stats = stats;
    }

    /// <summary>Tổng quan key OpenRouter (usage, usage_daily, usage_weekly, usage_monthly, limit_remaining, …).</summary>
    [HttpGet("openrouter-key")]
    public async Task<IActionResult> GetOpenRouterKey(CancellationToken cancellationToken)
    {
        if (!_stats.IsOpenRouterConfigured())
        {
            return Ok(new
            {
                available = false,
                message = "Cấu hình AI không trỏ tới OpenRouter (BaseUrl/EmbeddingBaseUrl không chứa openrouter.ai)."
            });
        }

        var (ok, error, data) = await _stats.GetOpenRouterKeyStatsAsync(cancellationToken).ConfigureAwait(false);
        if (!ok)
            return StatusCode(502, new { available = true, message = error });

        return Ok(new { available = true, data });
    }

    /// <summary>Danh sách API key OpenRouter + usage/limit từng key (GET /api/v1/keys, cần Management key).</summary>
    [HttpGet("openrouter-keys")]
    public async Task<IActionResult> GetOpenRouterKeysList(CancellationToken cancellationToken)
    {
        if (!_stats.IsOpenRouterConfigured())
        {
            return Ok(new
            {
                available = false,
                message = "Cấu hình AI không trỏ tới OpenRouter."
            });
        }

        var (ok, error, keys) = await _stats.GetOpenRouterKeysListAsync(cancellationToken).ConfigureAwait(false);
        if (!ok)
            return StatusCode(502, new { available = true, message = error, keys = Array.Empty<object>() });

        return Ok(new { available = true, keys });
    }

    /// <summary>Chi tiết một API key theo hash (GET /api/v1/keys/{{hash}}, cần Management key).</summary>
    [HttpGet("openrouter-keys/{hash}")]
    public async Task<IActionResult> GetOpenRouterKeyByHash(string hash, CancellationToken cancellationToken)
    {
        if (!_stats.IsOpenRouterConfigured())
        {
            return Ok(new { available = false, message = "Cấu hình AI không trỏ tới OpenRouter." });
        }

        var (ok, error, json) = await _stats.GetOpenRouterKeyByHashRawJsonAsync(hash, cancellationToken).ConfigureAwait(false);
        if (!ok)
            return StatusCode(502, new { message = error });

        return Content(json ?? "{}", "application/json");
    }

    /// <summary>Analytics account-level (GET /api/v1/activity, giống trang Activity trên OpenRouter; cần Management key).</summary>
    [HttpGet("openrouter-activity")]
    public async Task<IActionResult> GetOpenRouterActivity(
        [FromQuery] string? date,
        [FromQuery] string? api_key_hash,
        [FromQuery] string? user_id,
        CancellationToken cancellationToken)
    {
        if (!_stats.IsOpenRouterConfigured())
        {
            return Ok(new { available = false, message = "Cấu hình AI không trỏ tới OpenRouter." });
        }

        var (ok, error, json) = await _stats
            .GetOpenRouterActivityRawJsonAsync(date, api_key_hash, user_id, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            return StatusCode(502, new { message = error });

        return Content(json ?? "{}", "application/json");
    }

    /// <summary>Credits OpenRouter: đã nạp / đã dùng (GET /api/v1/credits, cần Management key).</summary>
    [HttpGet("openrouter-credits")]
    public async Task<IActionResult> GetOpenRouterCredits(CancellationToken cancellationToken)
    {
        if (!_stats.IsOpenRouterConfigured())
        {
            return Ok(new { available = false, message = "Cấu hình AI không trỏ tới OpenRouter." });
        }

        var (ok, error, data) = await _stats.GetOpenRouterCreditsAsync(cancellationToken).ConfigureAwait(false);
        if (!ok)
            return StatusCode(502, new { available = true, message = error });

        return Ok(new { available = true, data });
    }

    /// <summary>Danh sách log từng lần gọi AI (token, model, generation_id, user…).</summary>
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? actionType,
        [FromQuery] string? modelName,
        [FromQuery] string? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var pageDto = await _stats
            .GetRequestLogsAsync(fromUtc, toUtc, actionType, modelName, status, page, pageSize, cancellationToken)
            .ConfigureAwait(false);
        return Ok(pageDto);
    }

    /// <summary>Số request theo ngày UTC (biểu đồ Generations), cùng bộ lọc với GET requests.</summary>
    [HttpGet("generations-daily")]
    public async Task<IActionResult> GetGenerationsDaily(
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? modelName,
        [FromQuery] string? status,
        [FromQuery] string? actionType,
        CancellationToken cancellationToken = default)
    {
        var to = toUtc ?? DateTime.UtcNow;
        var from = fromUtc ?? to.AddDays(-7);
        var dto = await _stats
            .GetGenerationsDailyCountsAsync(from, to, modelName, status, actionType, cancellationToken)
            .ConfigureAwait(false);
        return Ok(dto);
    }

    /// <summary>Metadata OpenRouter cho một generation (theo id trả về từ chat completion).</summary>
    [HttpGet("openrouter-generation/{generationId}")]
    public async Task<IActionResult> GetOpenRouterGeneration(string generationId, CancellationToken cancellationToken)
    {
        if (!_stats.IsOpenRouterConfigured())
        {
            return Ok(new
            {
                available = false,
                message = "Cấu hình AI không trỏ tới OpenRouter."
            });
        }

        var (ok, error, json) = await _stats.GetOpenRouterGenerationRawJsonAsync(generationId, cancellationToken)
            .ConfigureAwait(false);
        if (!ok)
            return StatusCode(502, new { message = error });

        return Content(json ?? "{}", "application/json");
    }
}
