using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace AIStory.API.BackgroundServices;

/// <summary>Định kỳ áp dụng quy tắc gia hạn token AI tác giả (theo chu kỳ UTC đã cấu hình).</summary>
public sealed class AuthorAiTokenAutoGrantBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthorAiTokenAutoGrantBackgroundService> _logger;

    public AuthorAiTokenAutoGrantBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<AuthorAiTokenAutoGrantBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _config.GetValue<int?>("AuthorAiTokenAutoGrant:CheckIntervalSeconds") ?? 600;
        intervalSeconds = Math.Clamp(intervalSeconds, 60, 86400);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IAuthorAiTokenAutoGrantService>();
                await svc.ProcessDueRulesAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[AuthorAiTokenAutoGrant] Scheduled sweep failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }
    }
}
