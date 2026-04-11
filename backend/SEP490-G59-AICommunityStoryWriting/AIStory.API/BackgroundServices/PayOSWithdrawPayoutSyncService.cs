using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Services.Helpers;
using Services.Integrations.PayOS;

namespace AIStory.API.BackgroundServices
{
    public sealed class PayOSWithdrawPayoutSyncService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IConfiguration _config;
        private readonly ILogger<PayOSWithdrawPayoutSyncService> _logger;

        public PayOSWithdrawPayoutSyncService(
            IServiceScopeFactory scopeFactory,
            IConfiguration config,
            ILogger<PayOSWithdrawPayoutSyncService> logger)
        {
            _scopeFactory = scopeFactory;
            _config = config;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var intervalSeconds = _config.GetValue<int?>("PayOS:WithdrawSyncIntervalSeconds") ?? 180;
            intervalSeconds = Math.Clamp(intervalSeconds, 30, 3600);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await SyncProcessingWithdrawsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[PayOS] Withdraw payout background sync failed");
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

        private async Task SyncProcessingWithdrawsAsync(CancellationToken cancellationToken)
        {
            var batchSize = _config.GetValue<int?>("PayOS:WithdrawSyncBatchSize") ?? 25;
            batchSize = Math.Clamp(batchSize, 1, 200);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<StoryPlatformDbContext>();
            var payos = scope.ServiceProvider.GetRequiredService<PayOSClient>();

            var processing = await db.withdraw_requests
                // Use ToUpper() form to ensure EF can translate properly.
                .Where(w => w.status != null && w.status.Trim().ToUpper() == "PROCESSING")
                .Where(w => !string.IsNullOrWhiteSpace(w.transaction_proof_url))
                .OrderBy(w => w.created_at)
                .Take(batchSize)
                .ToListAsync(cancellationToken);

            if (processing.Count == 0) return;

            _logger.LogInformation("[PayOS] Syncing {Count} processing withdraws", processing.Count);

            foreach (var req in processing)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var strategy = db.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);

                    var current = await db.withdraw_requests
                        .FirstOrDefaultAsync(x => x.id == req.id, cancellationToken);

                    if (current == null)
                    {
                        await tx.CommitAsync(cancellationToken);
                        return;
                    }

                    if (string.IsNullOrWhiteSpace(current.status) || !string.Equals(current.status.Trim(), "PROCESSING", StringComparison.OrdinalIgnoreCase))
                    {
                        await tx.CommitAsync(cancellationToken);
                        return;
                    }

                    var payoutId = current.transaction_proof_url;
                    if (string.IsNullOrWhiteSpace(payoutId))
                    {
                        await tx.CommitAsync(cancellationToken);
                        return;
                    }

                    PayOSClient.GetPayoutResult payout;
                    try
                    {
                        payout = await payos.GetPayoutInfoAsync(payoutId!, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "[PayOS] Failed to get payout info: payoutId={PayoutId}", payoutId);
                        await tx.CommitAsync(cancellationToken);
                        return;
                    }

                    var approvalState = AuthorWithdrawWalletHelper.MapPayOSPayoutToSettlementStatus(
                        payout.ApprovalState,
                        payout.FirstTransactionState);

                    // Not terminal -> keep PROCESSING
                    if (approvalState == null)
                    {
                        _logger.LogInformation(
                            "[PayOS] payout not terminal. payoutId={PayoutId}, approvalState={ApprovalState}, firstTx={FirstTx}",
                            payoutId,
                            payout.ApprovalState,
                            payout.FirstTransactionState);
                        await tx.CommitAsync(cancellationToken);
                        return;
                    }

                    var now = DateTime.UtcNow;
                    current.processed_at = now;

                    if (!current.author_id.HasValue)
                    {
                        current.status = approvalState;
                        await db.SaveChangesAsync(cancellationToken);
                        await tx.CommitAsync(cancellationToken);
                        return;
                    }

                    // Wallet bookkeeping:
                    // - COMPLETED: release frozen -> no refund
                    // - FAILED: refund frozen -> income_balance
                    var wallet = await db.wallets.FirstOrDefaultAsync(w => w.user_id == current.author_id.Value, cancellationToken);
                    if (wallet == null)
                    {
                        wallet = new wallets
                        {
                            user_id = current.author_id.Value,
                            balance_coin = 0,
                            currency = "VND",
                            income_balance = 0m,
                            frozen_balance = 0m,
                            pending_escrow_balance = 0m,
                            updated_at = now
                        };
                        db.wallets.Add(wallet);
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    if (approvalState == "COMPLETED")
                    {
                        AuthorWithdrawWalletHelper.ApplyPayoutCompleted(wallet, current.amount_requested);
                        current.status = "COMPLETED";
                    }
                    else
                    {
                        wallet.frozen_balance = Math.Max(0m, (wallet.frozen_balance ?? 0m) - current.amount_requested);
                        wallet.income_balance = (wallet.income_balance ?? 0m) + current.amount_requested;
                        current.status = "FAILED";
                    }

                    var oldStatus = req.status;
                    _logger.LogInformation(
                        "[PayOS] Updated withdraw req {WithdrawId}: {OldStatus} -> {NewStatus}, payoutId={PayoutId}, approvalState={ApprovalState}",
                        current.id,
                        oldStatus,
                        current.status,
                        payoutId,
                        approvalState
                    );

                    wallet.updated_at = now;
                    await db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                });
            }
        }
    }
}

