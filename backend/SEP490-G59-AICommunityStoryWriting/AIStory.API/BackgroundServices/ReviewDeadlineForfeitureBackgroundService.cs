using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace AIStory.API.BackgroundServices;

/// <summary>Định kỳ kiểm tra claim moderator quá hạn duyệt → trả về hàng đợi + ghi moderation_logs.</summary>
public sealed class ReviewDeadlineForfeitureBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _config;
    private readonly ILogger<ReviewDeadlineForfeitureBackgroundService> _logger;

    public ReviewDeadlineForfeitureBackgroundService(
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<ReviewDeadlineForfeitureBackgroundService> logger)
    {
        _scopeFactory = scopeFactory;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalSeconds = _config.GetValue<int?>("Moderation:ReviewDeadlineForfeitIntervalSeconds") ?? 120;
        intervalSeconds = Math.Clamp(intervalSeconds, 30, 3600);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<IReviewDeadlineForfeitureService>();
                svc.ProcessOverdueClaims();
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "[Moderation] Review deadline forfeit sweep failed");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // ignore
            }
        }
    }
}
