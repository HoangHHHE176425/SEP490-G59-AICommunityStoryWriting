using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.DTOs.Notifications;
using Services.DTOs.Payments;
using Services.Integrations.PayOS;
using Services.Interfaces;
using System.Security.Cryptography;
using System.Text.Json;

namespace Services.Implementations
{
    public class CoinPaymentService : ICoinPaymentService
    {
        private readonly StoryPlatformDbContext _db;
        private readonly PayOSClient _payos;
        private readonly IConfiguration _config;
        private readonly ILogger<CoinPaymentService> _logger;
        private readonly INotificationHubNotifier? _notificationHubNotifier;

        public CoinPaymentService(StoryPlatformDbContext db, PayOSClient payos, IConfiguration config, ILogger<CoinPaymentService> logger, INotificationHubNotifier? notificationHubNotifier = null)
        {
            _db = db;
            _payos = payos;
            _config = config;
            _logger = logger;
            _notificationHubNotifier = notificationHubNotifier;
        }

        public async Task<IReadOnlyList<CoinPackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default)
        {
            var list = await _db.coin_packages
                .AsNoTracking()
                .Where(p => (p.is_active ?? false) == true)
                .OrderBy(p => p.price_amount)
                .Select(p => new CoinPackageDto
                {
                    Id = p.id,
                    Name = p.name,
                    PriceAmount = p.price_amount,
                    Currency = p.currency ?? "VND",
                    CoinAmount = p.coin_amount,
                    BonusCoin = p.bonus_coin ?? 0,
                    IsActive = p.is_active ?? false
                })
                .ToListAsync(cancellationToken);

            return list;
        }

        public async Task<WalletDto> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == userId, cancellationToken);
            if (wallet == null)
            {
                wallet = new wallets
                {
                    user_id = userId,
                    balance_coin = 0,
                    currency = "VND",
                    income_balance = 0m,
                    frozen_balance = 0m,
                    pending_escrow_balance = 0m,
                    updated_at = DateTime.UtcNow
                };
                _db.wallets.Add(wallet);
                await _db.SaveChangesAsync(cancellationToken);
            }

            return new WalletDto
            {
                UserId = wallet.user_id,
                BalanceCoin = wallet.balance_coin ?? 0,
                Currency = wallet.currency ?? "VND",
                UpdatedAt = AsUtc(wallet.updated_at)
            };
        }

        public async Task<IReadOnlyList<CoinOrderDto>> GetMyOrdersAsync(Guid userId, int take = 50, CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 200);

            var orders = await _db.coin_orders
                .AsNoTracking()
                .Where(o => o.user_id == userId)
                .OrderByDescending(o => o.created_at)
                .Take(take)
                .ToListAsync(cancellationToken);

            return orders.Select(MapOrderDto).ToList();
        }

        public async Task<CreatePayOSPaymentResponseDto> CreatePayOSPaymentAsync(Guid userId, CreatePayOSPaymentRequestDto request, CancellationToken cancellationToken = default)
        {
            var pkg = await _db.coin_packages
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.id == request.PackageId, cancellationToken);

            if (pkg == null) throw new InvalidOperationException("Coin package not found.");
            if (!(pkg.is_active ?? false)) throw new InvalidOperationException("Coin package is not active.");

            var coinsGranted = pkg.coin_amount + (pkg.bonus_coin ?? 0);
            if (coinsGranted <= 0) throw new InvalidOperationException("Invalid coin package configuration.");
            if (pkg.price_amount <= 0) throw new InvalidOperationException("Invalid coin package price.");

            // Create order (PENDING)
            var order = new coin_orders
            {
                id = Guid.NewGuid(),
                user_id = userId,
                package_id = pkg.id,
                amount_paid = pkg.price_amount,
                coins_granted = coinsGranted,
                payment_gateway = "PAYOS",
                status = "PENDING",
                created_at = DateTime.UtcNow
            };

            _db.coin_orders.Add(order);
            await _db.SaveChangesAsync(cancellationToken);

            // Generate a PayOS orderCode (numeric) with low collision risk.
            var unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var suffix = RandomNumberGenerator.GetInt32(0, 1000);
            var orderCode = checked(unixSeconds * 1000 + suffix);

            var description = $"Nap coin: {pkg.name}";

            _logger.LogInformation("[PayOS] Creating payment link for coin_order={OrderId} user={UserId} orderCode={OrderCode}", order.id, userId, orderCode);

            // Ensure the redirect back contains the coinOrderId so the frontend can auto-sync status on return/cancel.
            var cancelUrl = QueryHelpers.AddQueryString(request.CancelUrl, "orderId", order.id.ToString());
            var returnUrl = QueryHelpers.AddQueryString(request.ReturnUrl, "orderId", order.id.ToString());

            // Set PayOS payment link expiration (TTL)
            // PayOS expects expiredAt as Unix Timestamp (Int32).
            var ttlMinutes = _config.GetValue<int?>("PayOS:DefaultExpiredMinutes") ?? 15;
            ttlMinutes = Math.Clamp(ttlMinutes, 1, 7 * 24 * 60); // 1 minute .. 7 days (sane bound)
            var expiredAt = checked((int)DateTimeOffset.UtcNow.AddMinutes(ttlMinutes).ToUnixTimeSeconds());

            var payosRes = await _payos.CreatePaymentLinkAsync(
                orderCode,
                pkg.price_amount,
                description,
                cancelUrl,
                returnUrl,
                expiredAt,
                cancellationToken
            );

            // Store PayOS identifiers for webhook correlation.
            order.gateway_transaction_id = payosRes.PaymentLinkId;
            order.gateway_response_code = payosRes.Code;
            await _db.SaveChangesAsync(cancellationToken);

            return new CreatePayOSPaymentResponseDto
            {
                CoinOrderId = order.id,
                PackageId = pkg.id,
                AmountPaid = pkg.price_amount,
                CoinsGranted = coinsGranted,
                OrderCode = orderCode,
                PaymentLinkId = payosRes.PaymentLinkId,
                CheckoutUrl = payosRes.CheckoutUrl
            };
        }

        public async Task<string> ProcessPayOSWebhookAsync(string rawBody, CancellationToken cancellationToken = default)
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;

            if (!root.TryGetProperty("signature", out var sigEl))
                throw new InvalidOperationException("Missing signature");
            if (!root.TryGetProperty("data", out var dataEl))
                throw new InvalidOperationException("Missing data");

            var signature = sigEl.ToString() ?? string.Empty;
            var expected = _payos.ComputeWebhookSignature(dataEl);

            if (!string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("[PayOS] Invalid signature. expected={Expected} actual={Actual}", expected, signature);
                throw new InvalidOperationException("Invalid signature");
            }

            var paymentLinkId =
                dataEl.TryGetProperty("paymentLinkId", out var pl) ? pl.ToString() :
                dataEl.TryGetProperty("payment_link_id", out var pl2) ? pl2.ToString() :
                null;

            if (string.IsNullOrWhiteSpace(paymentLinkId))
                throw new InvalidOperationException("Missing paymentLinkId");

            var code =
                dataEl.TryGetProperty("code", out var codeEl) ? codeEl.ToString() :
                root.TryGetProperty("code", out var rootCodeEl) ? rootCodeEl.ToString() :
                null;

            var isPaid = string.Equals(code, "00", StringComparison.OrdinalIgnoreCase);

            // SQL Server retry execution strategy doesn't allow user-initiated transactions directly.
            // Execute the whole unit (including transaction) inside the execution strategy.
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

                var order = await _db.coin_orders.FirstOrDefaultAsync(
                    o => o.payment_gateway == "PAYOS" && o.gateway_transaction_id == paymentLinkId,
                    cancellationToken);

                if (order == null)
                {
                    _logger.LogWarning("[PayOS] Webhook for unknown paymentLinkId={PaymentLinkId}", paymentLinkId);
                    await tx.CommitAsync(cancellationToken);
                    return "IGNORED_UNKNOWN_ORDER";
                }

                if (string.Equals(order.status, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    await tx.CommitAsync(cancellationToken);
                    return "OK_ALREADY_PAID";
                }

                order.gateway_response_code = code;
                order.completed_at = DateTime.UtcNow;

                if (!isPaid)
                {
                    order.status = "FAILED";
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return "OK_FAILED";
                }

                // Ensure wallet exists
                var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == order.user_id, cancellationToken);
                if (wallet == null)
                {
                    wallet = new wallets
                    {
                        user_id = order.user_id,
                        balance_coin = 0,
                        currency = "VND",
                        income_balance = 0m,
                        frozen_balance = 0m,
                        pending_escrow_balance = 0m,
                        updated_at = DateTime.UtcNow
                    };
                    _db.wallets.Add(wallet);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                wallet.balance_coin = (wallet.balance_coin ?? 0) + order.coins_granted;
                wallet.updated_at = DateTime.UtcNow;

                order.status = "PAID";

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                _logger.LogInformation("[PayOS] Order paid. coin_order={OrderId} user={UserId} +coins={Coins}", order.id, order.user_id, order.coins_granted);
                return "OK_PAID";
            });
        }

        public async Task<CoinOrderDto> SyncMyPayOSOrderAsync(Guid userId, Guid coinOrderId, CancellationToken cancellationToken = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

                var order = await _db.coin_orders.FirstOrDefaultAsync(o => o.id == coinOrderId && o.user_id == userId, cancellationToken);
                if (order == null) throw new InvalidOperationException("Order not found.");
                if (!string.Equals(order.payment_gateway, "PAYOS", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Order is not a PayOS order.");

                var paymentLinkId = order.gateway_transaction_id;
                if (string.IsNullOrWhiteSpace(paymentLinkId))
                    throw new InvalidOperationException("Missing paymentLinkId for this order.");

                var payos = await _payos.GetPaymentRequestAsync(paymentLinkId!, cancellationToken);

                var payosStatus = (payos.Status ?? string.Empty).ToUpperInvariant();

                // If already PAID, keep idempotent
                if (string.Equals(order.status, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    await tx.CommitAsync(cancellationToken);
                    return MapOrderDto(order);
                }

                if (payosStatus == "PAID")
                {
                    // Ensure wallet exists
                    var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == order.user_id, cancellationToken);
                    if (wallet == null)
                    {
                        wallet = new wallets
                        {
                            user_id = order.user_id,
                            balance_coin = 0,
                            currency = "VND",
                            income_balance = 0m,
                            frozen_balance = 0m,
                            pending_escrow_balance = 0m,
                            updated_at = DateTime.UtcNow
                        };
                        _db.wallets.Add(wallet);
                        await _db.SaveChangesAsync(cancellationToken);
                    }

                    wallet.balance_coin = (wallet.balance_coin ?? 0) + order.coins_granted;
                    wallet.updated_at = DateTime.UtcNow;

                    order.status = "PAID";
                    order.gateway_response_code = "00";
                    order.completed_at = DateTime.UtcNow;

                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return MapOrderDto(order);
                }

                if (payosStatus is "CANCELLED" or "EXPIRED")
                {
                    order.status = payosStatus;
                    order.completed_at = (payos.CanceledAt?.UtcDateTime) ?? DateTime.UtcNow;
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    return MapOrderDto(order);
                }

                if (payosStatus == "PENDING")
                {
                    await tx.CommitAsync(cancellationToken);
                    return MapOrderDto(order);
                }

                // Unknown terminal state -> mark FAILED
                order.status = "FAILED";
                order.completed_at = DateTime.UtcNow;
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return MapOrderDto(order);
            });
        }

        public async Task<DonateResponseDto> DonateAsync(Guid senderUserId, Guid receiverUserId, int amount, string? message, CancellationToken cancellationToken = default)
        {
            if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount), "Số coin ủng hộ phải lớn hơn 0.");
            if (senderUserId == receiverUserId) throw new InvalidOperationException("Bạn không thể tự ủng hộ chính mình.");

            var strategy = _db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

                var sender = await _db.users.FirstOrDefaultAsync(u => u.id == senderUserId, cancellationToken);
                var receiver = await _db.users.FirstOrDefaultAsync(u => u.id == receiverUserId, cancellationToken);

                if (sender == null) throw new InvalidOperationException("Tài khoản người ủng hộ không tồn tại.");
                if (receiver == null) throw new InvalidOperationException("Tác giả nhận ủng hộ không tồn tại.");

                // Ensure wallets
                var senderWallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == senderUserId, cancellationToken);
                if (senderWallet == null)
                {
                    senderWallet = new wallets
                    {
                        user_id = senderUserId,
                        balance_coin = 0,
                        currency = "VND",
                        income_balance = 0m,
                        frozen_balance = 0m,
                        pending_escrow_balance = 0m,
                        updated_at = DateTime.UtcNow
                    };
                    _db.wallets.Add(senderWallet);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var receiverWallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == receiverUserId, cancellationToken);
                if (receiverWallet == null)
                {
                    receiverWallet = new wallets
                    {
                        user_id = receiverUserId,
                        balance_coin = 0,
                        currency = "VND",
                        income_balance = 0m,
                        frozen_balance = 0m,
                        pending_escrow_balance = 0m,
                        updated_at = DateTime.UtcNow
                    };
                    _db.wallets.Add(receiverWallet);
                    await _db.SaveChangesAsync(cancellationToken);
                }

                var senderBalance = senderWallet.balance_coin ?? 0;
                if (senderBalance < amount)
                {
                    throw new InvalidOperationException("Số dư coin không đủ để thực hiện ủng hộ.");
                }

                senderWallet.balance_coin = senderBalance - amount;
                senderWallet.updated_at = DateTime.UtcNow;

                var receiverBalance = receiverWallet.balance_coin ?? 0;
                receiverWallet.balance_coin = receiverBalance + amount;
                receiverWallet.updated_at = DateTime.UtcNow;

                var donation = new donations
                {
                    id = Guid.NewGuid(),
                    sender_id = senderUserId,
                    receiver_id = receiverUserId,
                    story_id = null,
                    amount = amount,
                    message = message,
                    created_at = DateTime.UtcNow
                };

                _db.donations.Add(donation);
                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                // B11: Thông báo realtime cho tác giả khi có donate.
                // Tuy nhiên KHÔNG để lỗi notification làm hỏng giao dịch donate (trừ/cộng coin + lưu donations).
                try
                {
                    var senderDisplayName = NotificationDAO.GetUserDisplayName(senderUserId);
                    var notification = NotificationDAO.NotifyDonationReceived(receiverUserId, senderDisplayName, amount, message);
                    _ = PushDonationNotificationAsync(receiverUserId, notification);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "DonateAsync succeeded but creating/pushing donation notification failed. Sender={SenderId} Receiver={ReceiverId} Amount={Amount}",
                        senderUserId, receiverUserId, amount);
                }

                return new DonateResponseDto
                {
                    DonationId = donation.id,
                    SenderId = senderUserId,
                    ReceiverId = receiverUserId,
                    Amount = amount,
                    Message = message,
                    CreatedAt = donation.created_at,
                    SenderBalanceAfter = senderWallet.balance_coin ?? 0,
                    ReceiverBalanceAfter = receiverWallet.balance_coin ?? 0
                };
            });
        }

        /// <summary>Gửi real-time (SignalR) thông báo donate tới tác giả. Gọi fire-and-forget sau DonateAsync.</summary>
        private async Task PushDonationNotificationAsync(Guid userId, notifications n)
        {
            if (_notificationHubNotifier == null) return;
            try
            {
                var dto = new NotificationDto
                {
                    Id = n.id,
                    Type = n.type,
                    Title = n.title,
                    Content = n.content,
                    LinkUrl = n.link_url,
                    IsRead = n.is_read == true,
                    CreatedAt = n.created_at
                };
                await _notificationHubNotifier.NotifyUserAsync(userId, dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push donation notification to author failed. UserId={UserId} NotificationId={NotificationId}", userId, n.id);
            }
        }

        private static CoinOrderDto MapOrderDto(coin_orders o)
        {
            return new CoinOrderDto
            {
                Id = o.id,
                PackageId = o.package_id,
                AmountPaid = o.amount_paid,
                CoinsGranted = o.coins_granted,
                Status = o.status ?? "PENDING",
                PaymentGateway = o.payment_gateway ?? "PAYOS",
                GatewayTransactionId = o.gateway_transaction_id,
                GatewayResponseCode = o.gateway_response_code,
                // Normalize to UTC so JSON includes 'Z' and frontend renders local time correctly.
                CreatedAt = AsUtc(o.created_at),
                CompletedAt = AsUtc(o.completed_at)
            };
        }

        private static DateTime? AsUtc(DateTime? dt)
        {
            if (dt == null) return null;
            var value = dt.Value;
            return value.Kind == DateTimeKind.Utc ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }
    }
}

