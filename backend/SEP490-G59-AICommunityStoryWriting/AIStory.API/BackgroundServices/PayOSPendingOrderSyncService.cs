using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Integrations.PayOS;

namespace AIStory.API.BackgroundServices
{
    public sealed class PayOSPendingOrderSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PayOSPendingOrderSyncService> _logger;

        public PayOSPendingOrderSyncService(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<PayOSPendingOrderSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = _config.GetValue<int?>("PayOS:PendingSyncIntervalSeconds") ?? 120;
            intervalSeconds = Math.Clamp(intervalSeconds, 30, 3600);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncPendingAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PayOS] Background sync failed");
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

        private async Task SyncPendingAsync(CancellationToken cancellationToken)
        {
            // We only try to clean up "old" pending orders to avoid hammering PayOS.
            var ttlMinutes = _config.GetValue<int?>("PayOS:DefaultExpiredMinutes") ?? 15;
            ttlMinutes = Math.Clamp(ttlMinutes, 1, 7 * 24 * 60);
            var graceMinutes = _config.GetValue<int?>("PayOS:PendingGraceMinutes") ?? 2; // allow PayOS to settle
            graceMinutes = Math.Clamp(graceMinutes, 0, 60);

            var cutoff = DateTime.UtcNow.AddMinutes(-(ttlMinutes + graceMinutes));
            var batchSize = _config.GetValue<int?>("PayOS:PendingSyncBatchSize") ?? 25;
            batchSize = Math.Clamp(batchSize, 1, 200);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StoryPlatformDbContext>();
            var payos = scope.ServiceProvider.GetRequiredService<PayOSClient>();

            var pending = await db.coin_orders
                .Where(o =>
                    o.payment_gateway == "PAYOS" &&
                    (o.status ?? "PENDING") == "PENDING" &&
                    o.created_at < cutoff &&
                    o.gateway_transaction_id != null &&
                    o.gateway_transaction_id != "")
                .OrderBy(o => o.created_at)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (pending.Count == 0) return;

            _logger.LogInformation("[PayOS] Background syncing {Count} pending orders older than {Cutoff:u}", pending.Count, cutoff);

            foreach (var order in pending)
            {
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var strategy = db.Database.CreateExecutionStrategy();
                    await strategy.ExecuteAsync(async () =>
                    {
                        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

                        var current = await db.coin_orders.FirstOrDefaultAsync(x => x.id == order.id, cancellationToken);
                        if (current == null)
                        {
                            await tx.CommitAsync(cancellationToken);
                            return;
                        }
                        if (!string.Equals(current.status, "PENDING", StringComparison.OrdinalIgnoreCase))
                        {
                            await tx.CommitAsync(cancellationToken);
                            return;
                        }

                        var payosRes = await payos.GetPaymentRequestAsync(current.gateway_transaction_id!, cancellationToken);
                        var payosStatus = (payosRes.Status ?? string.Empty).ToUpperInvariant();

                        if (payosStatus == "PAID")
                        {
                            var wallet = await db.wallets.FirstOrDefaultAsync(w => w.user_id == current.user_id, cancellationToken);
                            if (wallet == null)
                            {
                                wallet = new BusinessObjects.Entities.wallets
                                {
                                    user_id = current.user_id,
                                    balance_coin = 0,
                                    currency = "VND",
                                    income_balance = 0m,
                                    frozen_balance = 0m,
                                    pending_escrow_balance = 0m,
                                    updated_at = DateTime.UtcNow
                                };
                                db.wallets.Add(wallet);
                                await db.SaveChangesAsync(cancellationToken);
                            }

                            wallet.balance_coin = (wallet.balance_coin ?? 0) + current.coins_granted;
                            wallet.updated_at = DateTime.UtcNow;

                            current.status = "PAID";
                            current.gateway_response_code = "00";
                            current.completed_at = DateTime.UtcNow;

                            await db.SaveChangesAsync(cancellationToken);
                            await tx.CommitAsync(cancellationToken);
                            return;
                        }

                        if (payosStatus is "CANCELLED" or "EXPIRED")
                        {
                            current.status = payosStatus;
                            current.completed_at = (payosRes.CanceledAt?.UtcDateTime) ?? DateTime.UtcNow;
                            await db.SaveChangesAsync(cancellationToken);
                            await tx.CommitAsync(cancellationToken);
                            return;
                        }

                        if (payosStatus == "PENDING")
                        {
                            await tx.CommitAsync(cancellationToken);
                            return;
                        }

                        // Unknown terminal state -> mark FAILED
                        current.status = "FAILED";
                        current.completed_at = DateTime.UtcNow;
                        await db.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PayOS] Pending order sync failed for coin_order={OrderId}", order.id);
                }
            }
        }
    }
}

