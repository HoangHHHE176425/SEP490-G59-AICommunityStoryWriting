using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/admin/wallet")]
    [Authorize(Roles = "ADMIN")]
    public sealed class AdminWalletController : ControllerBase
    {
        private readonly StoryPlatformDbContext _db;
        private const string PlatformWalletAdjustPurchaseType = "PLATFORM_WALLET_ADJ";
        private const string UserWalletAdjustPurchaseType = "USR_WALLET_ADJ";

        public AdminWalletController(StoryPlatformDbContext db)
        {
            _db = db;
        }

        private Guid? GetCurrentAdminId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        /// <summary>Số dư coin hiện tại của ví hệ thống (platform_wallet id=1).</summary>
        [HttpGet("balance")]
        public async Task<IActionResult> GetPlatformWalletBalance(CancellationToken cancellationToken)
        {
            var w = await _db.platform_wallet.AsNoTracking().FirstOrDefaultAsync(x => x.id == 1, cancellationToken);
            return Ok(new
            {
                balanceCoin = w?.balance_coin ?? 0,
                updatedAt = w?.updated_at
            });
        }

        /// <summary>
        /// Tổng quan ví hệ thống (để FE admin hiển thị dashboard).
        /// Lưu ý: Các số liệu aggregate dựa trên dữ liệu hiện có trong DB schema hiện tại.
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
        {
            var platform = await _db.platform_wallet.AsNoTracking().FirstOrDefaultAsync(x => x.id == 1, cancellationToken);

            // Fixed conversion rate: 100 coin = 10,000 VND => 1 coin = 100 VND
            const decimal coinRateVnd = 100m;

            var totals = await _db.wallets
                .AsNoTracking()
                .GroupBy(_ => 1)
                .Select(g => new
                {
                    // Total "coin state" for users/authors:
                    // - balance_coin: spendable
                    // - income_balance: withdrawable income
                    // - frozen_balance: coins frozen while pending withdrawal approval
                    // - pending_escrow_balance: coins pending reconciliation
                    TotalCoinsInSystem =
                        (g.Sum(x => (decimal?)x.balance_coin) ?? 0m)
                        + (g.Sum(x => x.income_balance) ?? 0m)
                        + (g.Sum(x => x.frozen_balance) ?? 0m)
                        + (g.Sum(x => x.pending_escrow_balance) ?? 0m),
                    TotalIncomeBalance = g.Sum(x => (decimal?)x.income_balance) ?? 0m,
                    TotalFrozenBalance = g.Sum(x => (decimal?)x.frozen_balance) ?? 0m,
                    TotalPendingEscrow = g.Sum(x => (decimal?)x.pending_escrow_balance) ?? 0m
                })
                .FirstOrDefaultAsync(cancellationToken);

            var totalRechargeVnd = await _db.coin_orders
                .AsNoTracking()
                .Where(o => (o.status ?? "PENDING") == "PAID")
                .SumAsync(o => (decimal?)o.amount_paid, cancellationToken) ?? 0m;

            // NOTE: withdraw_requests.amount_requested đang là decimal(15,2) nhưng hiện bạn dùng như "coin" trong CreateWithdrawRequestAsync.
            // Dashboard admin hiện tại đang hiển thị VND (mock), nhưng dữ liệu thực tế hiện tại là COIN.
            // Vì chưa có cơ chế quy đổi coin -> VND ở luồng rút, endpoint sẽ trả rõ cả 2 field.
            var totalWithdrawCoins = await _db.withdraw_requests
                .AsNoTracking()
                .Where(w => (w.status ?? "PENDING") == "SUCCESS")
                .SumAsync(w => (decimal?)w.amount_requested, cancellationToken) ?? 0m;

            var platformFeeCoins = (decimal)(platform?.balance_coin ?? 0);
            var platformFeeVnd = platformFeeCoins * coinRateVnd;
            var totalWithdrawVnd = totalWithdrawCoins * coinRateVnd;

            var activeAuthors = await _db.users
                .AsNoTracking()
                .CountAsync(u => (u.role ?? "") == "AUTHOR" && (u.status ?? "") == "ACTIVE", cancellationToken);

            var activeReaders = await _db.users
                .AsNoTracking()
                .CountAsync(u => (u.role ?? "") == "USER" && (u.status ?? "") == "ACTIVE", cancellationToken);

            return Ok(new
            {
                coinRateVnd = coinRateVnd,
                systemWalletBalanceCoins = platform?.balance_coin ?? 0,
                totalCoinsInSystem = totals?.TotalCoinsInSystem ?? 0,
                totalIncomeBalance = totals?.TotalIncomeBalance ?? 0m,
                totalFrozenBalance = totals?.TotalFrozenBalance ?? 0m,
                totalPendingEscrow = totals?.TotalPendingEscrow ?? 0m,
                totalRechargeVnd = totalRechargeVnd,

                // New, correct fields:
                totalWithdrawCoins = totalWithdrawCoins,
                totalWithdrawVnd = totalWithdrawVnd,
                platformFeeCoins = platformFeeCoins,
                platformRevenueVnd = platformFeeVnd,

                activeAuthors,
                activeReaders
            });
        }

        public sealed class AdminPlatformWalletAdjustRequest
        {
            /// <summary>
            /// Số coin cộng (delta &gt; 0) hoặc trừ (delta &lt; 0) cho ví hệ thống.
            /// </summary>
            public int DeltaCoins { get; set; }

            /// <summary>
            /// Lý do điều chỉnh (lưu trong escrow_status dạng ADD:note hoặc SUB:note).
            /// Do escrow_status chỉ có max length 20 nên note sẽ bị cắt ngắn.
            /// </summary>
            public string? Note { get; set; }
        }

        public sealed class AdminUserWalletAdjustRequest
        {
            /// <summary>
            /// Định danh user cần điều chỉnh: có thể là Guid userId, hoặc email, hoặc nickname.
            /// </summary>
            public string TargetUser { get; set; } = null!;

            /// <summary>
            /// Số coin cộng (delta &gt; 0) hoặc trừ (delta &lt; 0).
            /// </summary>
            public int DeltaCoins { get; set; }

            /// <summary>
            /// Lý do điều chỉnh (lưu trong escrow_status dưới dạng ADD:note hoặc SUB:note).
            /// </summary>
            public string? Note { get; set; }
        }

        private async Task<Guid?> ResolveTargetUserIdAsync(string target, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(target)) return null;
            var trimmed = target.Trim();

            if (Guid.TryParse(trimmed, out var guid))
                return guid;

            // Try by email first.
            var byEmail = await _db.users.AsNoTracking()
                .Where(u => u.email == trimmed)
                .Select(u => (Guid?)u.id)
                .FirstOrDefaultAsync(cancellationToken);
            if (byEmail.HasValue) return byEmail.Value;

            // Try by nickname.
            var byNickname = await _db.user_profiles.AsNoTracking()
                .Where(p => p.nickname == trimmed)
                .Select(p => (Guid?)p.user_id)
                .FirstOrDefaultAsync(cancellationToken);

            return byNickname;
        }

        /// <summary>Admin điều chỉnh trực tiếp số dư ví hệ thống (platform_wallet id=1).</summary>
        [HttpPost("adjust")]
        public async Task<IActionResult> AdjustPlatformWallet([FromBody] AdminPlatformWalletAdjustRequest request, CancellationToken cancellationToken)
        {
            if (request == null) return BadRequest(new { message = "Payload không hợp lệ." });
            if (request.DeltaCoins == 0) return BadRequest(new { message = "DeltaCoins không được bằng 0." });

            var adminId = GetCurrentAdminId();

            // Important: SqlServerRetryingExecutionStrategy does NOT support user-initiated transactions
            // unless the whole block is executed inside CreateExecutionStrategy().ExecuteAsync(...)
            var executionStrategy = _db.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var platform = await _db.platform_wallet.FirstOrDefaultAsync(x => x.id == 1, cancellationToken);
                    if (platform == null)
                    {
                        platform = new platform_wallet
                        {
                            id = 1,
                            balance_coin = 0,
                            updated_at = DateTime.UtcNow
                        };
                        _db.platform_wallet.Add(platform);
                        await _db.SaveChangesAsync(cancellationToken);
                    }

                    var newBalance = platform.balance_coin + request.DeltaCoins;
                    if (newBalance < 0)
                    {
                        return (IActionResult)BadRequest(new { message = "Số dư ví hệ thống không đủ để trừ.", currentBalance = platform.balance_coin });
                    }

                    platform.balance_coin = newBalance;
                    platform.updated_at = DateTime.UtcNow;
                    _db.platform_wallet.Update(platform);

                    // Audit bằng cách ghi vào purchases với purchase_type riêng.
                    var sign = request.DeltaCoins >= 0 ? "ADD" : "SUB";
                    var notePart = string.Empty;
                    if (!string.IsNullOrWhiteSpace(request.Note))
                    {
                        // escrow_status max length = 20; prefix "ADD:"/ "SUB:" length = 4 => notePart max = 16
                        notePart = request.Note.Trim();
                        if (notePart.Length > 16) notePart = notePart.Substring(0, 16);
                    }

                    _db.purchases.Add(new purchases
                    {
                        id = Guid.NewGuid(),
                        user_id = adminId,
                        story_id = null,
                        chapter_id = null,
                        price_paid = Math.Abs(request.DeltaCoins),
                        // Note: purchases.purchase_type column in DB appears to be length-constrained.
                        // Using a shorter value to avoid SqlException "String or binary data would be truncated".
                        purchase_type = PlatformWalletAdjustPurchaseType,
                        escrow_status = sign + (string.IsNullOrWhiteSpace(notePart) ? string.Empty : (":" + notePart)),
                        released_at = DateTime.UtcNow,
                        platform_fee_ratio = null,
                        created_at = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    return (IActionResult)Ok(new
                    {
                        balanceCoin = newBalance,
                        updatedAt = platform.updated_at
                    });
                }
                catch
                {
                    try { await tx.RollbackAsync(cancellationToken); } catch { /* ignore */ }
                    throw;
                }
            });
        }

        /// <summary>
        /// Lịch sử điều chỉnh ví hệ thống (dùng cho FE admin hiển thị).
        /// </summary>
        [HttpGet("adjustments")]
        public async Task<IActionResult> GetPlatformWalletAdjustments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? type = null, // ALL | ADD | SUB
            [FromQuery] string? q = null,    // search in note
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

            var fromDate = dateFrom?.Date;
            var toExclusive = dateTo?.Date.AddDays(1);

            var query = _db.purchases.AsNoTracking()
                .Where(p => p.purchase_type == PlatformWalletAdjustPurchaseType);

            if (fromDate.HasValue)
                query = query.Where(p => (p.released_at ?? p.created_at) >= fromDate.Value);
            if (toExclusive.HasValue)
                query = query.Where(p => (p.released_at ?? p.created_at) < toExclusive.Value);

            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var t = type.ToUpperInvariant();
                if (t == "ADD")
                    query = query.Where(p => ((p.escrow_status ?? string.Empty).ToUpper()).StartsWith("ADD"));
                else if (t == "SUB")
                    query = query.Where(p => ((p.escrow_status ?? string.Empty).ToUpper()).StartsWith("SUB"));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qUpper = q.ToUpperInvariant();
                query = query.Where(p => ((p.escrow_status ?? string.Empty).ToUpper()).Contains(qUpper));
            }

            var total = await query.CountAsync(cancellationToken);

            var rawItems = await query
                .OrderByDescending(p => p.released_at ?? p.created_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    PurchaseId = p.id,
                    AdjustedAt = p.released_at ?? p.created_at,
                    PricePaid = p.price_paid,
                    EscrowStatus = p.escrow_status,
                    AdminId = p.user_id
                })
                .ToListAsync(cancellationToken);

            var items = rawItems.Select(x =>
            {
                var escrow = x.EscrowStatus ?? string.Empty;
                var escrowUpper = escrow.ToUpperInvariant();
                var isSub = escrowUpper.StartsWith("SUB");
                var delta = isSub ? -x.PricePaid : x.PricePaid;

                string? note = null;
                var idx = escrow.IndexOf(':');
                if (idx >= 0 && idx < escrow.Length - 1)
                    note = escrow.Substring(idx + 1);

                return new
                {
                    PurchaseId = x.PurchaseId,
                    AdjustedAt = x.AdjustedAt,
                    DeltaCoins = delta,
                    AdminId = x.AdminId,
                    Note = note ?? string.Empty
                };
            }).ToList();

            return Ok(new
            {
                totalCount = total,
                page = page,
                pageSize = pageSize,
                items = items
            });
        }

        /// <summary>
        /// Admin điều chỉnh ví người dùng (wallets.balance_coin).
        /// Chỉ thay đổi một user wallet để tránh gây sai lệch “trừ của ai”.
        /// purchase_type: USR_WALLET_ADJ
        /// </summary>
        [HttpPost("adjust-user-wallet")]
        public async Task<IActionResult> AdjustUserWallet(
            [FromBody] AdminUserWalletAdjustRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null) return BadRequest(new { message = "Payload không hợp lệ." });
            if (request.DeltaCoins == 0) return BadRequest(new { message = "DeltaCoins không được bằng 0." });
            if (string.IsNullOrWhiteSpace(request.TargetUser))
                return BadRequest(new { message = "TargetUser là bắt buộc." });

            var targetUserId = await ResolveTargetUserIdAsync(request.TargetUser, cancellationToken);
            if (!targetUserId.HasValue)
                return BadRequest(new { message = "Không tìm thấy user để điều chỉnh." });

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == targetUserId.Value, cancellationToken);
                    if (wallet == null)
                    {
                        wallet = new wallets
                        {
                            user_id = targetUserId.Value,
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

                    var current = wallet.balance_coin ?? 0;
                    var newBalance = current + request.DeltaCoins;
                    if (newBalance < 0)
                        return (IActionResult)BadRequest(new { message = "Số dư không đủ để trừ.", currentBalance = current });

                    wallet.balance_coin = newBalance;
                    wallet.updated_at = DateTime.UtcNow;
                    _db.wallets.Update(wallet);

                    var sign = request.DeltaCoins >= 0 ? "ADD" : "SUB";
                    var notePart = string.Empty;
                    if (!string.IsNullOrWhiteSpace(request.Note))
                    {
                        notePart = request.Note.Trim();
                        if (notePart.Length > 16) notePart = notePart.Substring(0, 16);
                    }

                    _db.purchases.Add(new purchases
                    {
                        id = Guid.NewGuid(),
                        user_id = targetUserId.Value,
                        story_id = null,
                        chapter_id = null,
                        price_paid = Math.Abs(request.DeltaCoins),
                        purchase_type = UserWalletAdjustPurchaseType,
                        escrow_status = sign + (string.IsNullOrWhiteSpace(notePart) ? string.Empty : (":" + notePart)),
                        released_at = DateTime.UtcNow,
                        platform_fee_ratio = null,
                        created_at = DateTime.UtcNow
                    });

                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    return (IActionResult)Ok(new
                    {
                        userId = targetUserId.Value,
                        balanceCoin = newBalance,
                        updatedAt = wallet.updated_at
                    });
                }
                catch
                {
                    try { await tx.RollbackAsync(cancellationToken); } catch { /* ignore */ }
                    throw;
                }
            });
        }

        /// <summary>History điều chỉnh ví người dùng.</summary>
        [HttpGet("user-adjustments")]
        public async Task<IActionResult> GetUserWalletAdjustments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? type = null, // ALL | ADD | SUB
            [FromQuery] string? q = null,    // search in note (escrow_status)
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            type = string.IsNullOrWhiteSpace(type) ? null : type.Trim();
            q = string.IsNullOrWhiteSpace(q) ? null : q.Trim();

            var fromDate = dateFrom?.Date;
            var toExclusive = dateTo?.Date.AddDays(1);

            var query = _db.purchases.AsNoTracking()
                .Where(p => p.purchase_type == UserWalletAdjustPurchaseType);

            if (fromDate.HasValue)
                query = query.Where(p => (p.released_at ?? p.created_at) >= fromDate.Value);
            if (toExclusive.HasValue)
                query = query.Where(p => (p.released_at ?? p.created_at) < toExclusive.Value);

            if (!string.IsNullOrWhiteSpace(type) && !string.Equals(type, "ALL", StringComparison.OrdinalIgnoreCase))
            {
                var t = type.ToUpperInvariant();
                if (t == "ADD")
                    query = query.Where(p => ((p.escrow_status ?? string.Empty).ToUpper()).StartsWith("ADD"));
                else if (t == "SUB")
                    query = query.Where(p => ((p.escrow_status ?? string.Empty).ToUpper()).StartsWith("SUB"));
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qUpper = q.ToUpperInvariant();
                query = query.Where(p => ((p.escrow_status ?? string.Empty).ToUpper()).Contains(qUpper));
            }

            var total = await query.CountAsync(cancellationToken);

            var rawItems = await query
                .OrderByDescending(p => p.released_at ?? p.created_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new
                {
                    PurchaseId = p.id,
                    AdjustedAt = p.released_at ?? p.created_at,
                    PricePaid = p.price_paid,
                    EscrowStatus = p.escrow_status,
                    UserId = p.user_id
                })
                .ToListAsync(cancellationToken);

            var items = rawItems.Select(x =>
            {
                var escrow = x.EscrowStatus ?? string.Empty;
                var escrowUpper = escrow.ToUpperInvariant();
                var isSub = escrowUpper.StartsWith("SUB");
                var delta = isSub ? -x.PricePaid : x.PricePaid;

                string? note = null;
                var idx = escrow.IndexOf(':');
                if (idx >= 0 && idx < escrow.Length - 1)
                    note = escrow.Substring(idx + 1);

                return new
                {
                    PurchaseId = x.PurchaseId,
                    AdjustedAt = x.AdjustedAt,
                    DeltaCoins = delta,
                    UserId = x.UserId,
                    Note = note ?? string.Empty
                };
            }).ToList();

            return Ok(new
            {
                totalCount = total,
                page = page,
                pageSize = pageSize,
                items = items
            });
        }

        /// <summary>
        /// Ledger hoạt động tiền (admin):
        /// - CHAPTER_UNLOCK: platform nhận 70%, buyer trả tiền, author nhận net vào income_balance
        /// - DONATE: ủng hộ tác giả — cùng cách chia 70% phí nền tảng / 30% thu nhập tác giả (CoinPaymentService.DonateAsync)
        /// - PLATFORM_WALLET_ADJ: điều chỉnh ví hệ thống
        /// - withdraw_requests: create/approve/reject (income_balance & frozen_balance)
        /// type=UNLOCK_AND_DONATE: chỉ mở khóa chương + donate (dùng cho tab lịch sử ví hệ thống).
        /// </summary>
        [HttpGet("system-coin-ledger")]
        public async Task<IActionResult> GetSystemCoinLedger(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null, // inclusive date
            [FromQuery] string? type = null, // ALL | UNLOCK | DONATE | UNLOCK_AND_DONATE | PLATFORM_ADJ | WITHDRAW_REQUESTED | WITHDRAW_APPROVED | WITHDRAW_REJECTED
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var fromDate = dateFrom?.Date;
            var toExclusive = dateTo?.Date.AddDays(1);

            type = string.IsNullOrWhiteSpace(type) ? "ALL" : type.Trim();
            var t = type.ToUpperInvariant();
            bool acceptAll = t == "ALL";

            // Phân nhánh rõ: UNLOCK_AND_DONATE không gồm điều chỉnh ví / rút tiền.
            var includeUnlock = acceptAll || t == "UNLOCK" || t == "UNLOCK_AND_DONATE";
            var includeDonate = acceptAll || t == "DONATE" || t == "UNLOCK_AND_DONATE";
            var includePlatformAdj = acceptAll || t == "PLATFORM_ADJ";
            var includeWithdrawReq = acceptAll || t == "WITHDRAW_REQUESTED";
            var includeWithdrawProcessed = acceptAll || t == "WITHDRAW_APPROVED" || t == "WITHDRAW_REJECTED";

            var parts = new List<IQueryable<SystemCoinLedgerItemDto>>();

            if (includeUnlock)
            {
                var unlockQ =
                    from p in _db.purchases.AsNoTracking()
                    where p.purchase_type == "CHAPTER_UNLOCK"
                        && p.chapter_id != null
                        && p.story_id != null
                    join c in _db.chapters.AsNoTracking() on p.chapter_id equals c.id
                    join s in _db.stories.AsNoTracking() on p.story_id equals s.id
                    where (fromDate == null || (p.released_at ?? p.created_at) >= fromDate.Value)
                        && (toExclusive == null || (p.released_at ?? p.created_at) < toExclusive.Value)
                    select new SystemCoinLedgerItemDto
                    {
                        EventType = "UNLOCK",
                        EventTime = p.released_at ?? p.created_at ?? DateTime.MinValue,
                        // Must match ChaptersController.UnlockPaidChapter:
                        // platformFee = (int)Math.Floor(coinPrice * 0.70m) => platform gets whole coins.
                        PlatformDeltaCoins = (decimal)Math.Floor(p.price_paid * 0.70m),
                        BuyerDeltaCoins = -(decimal)p.price_paid,
                        AuthorIncomeDeltaCoins = (decimal)(p.price_paid - Math.Floor(p.price_paid * 0.70m)),
                        AuthorFrozenDeltaCoins = 0m,
                        StoryId = s.id,
                        ChapterId = c.id,
                        StoryTitle = s.title ?? string.Empty,
                        ChapterTitle = c.title ?? string.Empty,
                        AdminId = null,
                        BuyerUserId = p.user_id,
                        AuthorUserId = s.author_id,
                        Note = null
                    };
                parts.Add(unlockQ);
            }

            if (includeDonate)
            {
                // Nguồn sự thật: author_income_logs (DONATE) + donations (tin nhắn, story). Khớp DonateAsync.
                var donateQ =
                    from log in _db.author_income_logs.AsNoTracking()
                    where log.source_type == "DONATE" && log.source_id != null
                    join d in _db.donations.AsNoTracking() on log.source_id equals d.id
                    join s in _db.stories.AsNoTracking() on d.story_id equals s.id into sj
                    from s in sj.DefaultIfEmpty()
                    where (fromDate == null || (log.created_at ?? DateTime.MinValue) >= fromDate.Value)
                        && (toExclusive == null || (log.created_at ?? DateTime.MinValue) < toExclusive.Value)
                    select new SystemCoinLedgerItemDto
                    {
                        EventType = "DONATE",
                        EventTime = log.created_at ?? d.created_at ?? DateTime.MinValue,
                        PlatformDeltaCoins = log.platform_fee ?? 0m,
                        BuyerDeltaCoins = -(log.gross_amount ?? (decimal)d.amount),
                        AuthorIncomeDeltaCoins = log.net_amount ?? 0m,
                        AuthorFrozenDeltaCoins = 0m,
                        StoryId = d.story_id,
                        ChapterId = null,
                        StoryTitle = s != null ? (s.title ?? string.Empty) : string.Empty,
                        ChapterTitle = string.Empty,
                        AdminId = null,
                        BuyerUserId = d.sender_id,
                        AuthorUserId = d.receiver_id,
                        Note = d.message
                    };
                parts.Add(donateQ);
            }

            if (includePlatformAdj)
            {
                var platformAdjQ =
                    from p in _db.purchases.AsNoTracking()
                    where p.purchase_type == PlatformWalletAdjustPurchaseType
                    where (fromDate == null || (p.released_at ?? p.created_at) >= fromDate.Value)
                        && (toExclusive == null || (p.released_at ?? p.created_at) < toExclusive.Value)
                    select new SystemCoinLedgerItemDto
                    {
                        EventType = "PLATFORM_ADJ",
                        EventTime = p.released_at ?? p.created_at ?? DateTime.MinValue,
                        PlatformDeltaCoins = ((p.escrow_status ?? string.Empty).ToUpper().StartsWith("SUB"))
                            ? -(decimal)p.price_paid
                            : (decimal)p.price_paid,
                        BuyerDeltaCoins = 0m,
                        AuthorIncomeDeltaCoins = 0m,
                        AuthorFrozenDeltaCoins = 0m,
                        StoryId = null,
                        ChapterId = null,
                        StoryTitle = string.Empty,
                        ChapterTitle = string.Empty,
                        AdminId = p.user_id,
                        BuyerUserId = null,
                        AuthorUserId = null,
                        Note = p.escrow_status
                    };
                parts.Add(platformAdjQ);
            }

            if (includeWithdrawReq)
            {
                var withdrawReqQ =
                    from w in _db.withdraw_requests.AsNoTracking()
                    where (fromDate == null || (w.created_at ?? w.processed_at) >= fromDate.Value)
                        && (toExclusive == null || (w.created_at ?? w.processed_at) < toExclusive.Value)
                    select new SystemCoinLedgerItemDto
                    {
                        EventType = "WITHDRAW_REQUESTED",
                        EventTime = w.created_at ?? w.processed_at ?? DateTime.MinValue,
                        PlatformDeltaCoins = 0m,
                        BuyerDeltaCoins = 0m,
                        AuthorIncomeDeltaCoins = -w.amount_requested,
                        AuthorFrozenDeltaCoins = w.amount_requested,
                        StoryId = null,
                        ChapterId = null,
                        StoryTitle = string.Empty,
                        ChapterTitle = string.Empty,
                        AdminId = null,
                        BuyerUserId = null,
                        AuthorUserId = w.author_id,
                        // admin_note chỉ được set khi admin approve/reject, nên giữ "requested" event không bị dính note sau này
                        Note = null
                    };
                parts.Add(withdrawReqQ);
            }

            if (includeWithdrawProcessed)
            {
                var withdrawProcessedQ =
                    from w in _db.withdraw_requests.AsNoTracking()
                    where w.processed_at != null
                        && (fromDate == null || w.processed_at >= fromDate.Value)
                        && (toExclusive == null || w.processed_at < toExclusive.Value)
                    select new SystemCoinLedgerItemDto
                    {
                        EventType = (w.status == "SUCCESS" || w.status == "COMPLETED") ? "WITHDRAW_APPROVED" : "WITHDRAW_REJECTED",
                        EventTime = w.processed_at!.Value,
                        PlatformDeltaCoins = 0m,
                        BuyerDeltaCoins = 0m,
                        AuthorIncomeDeltaCoins = (w.status == "SUCCESS" || w.status == "COMPLETED") ? 0m : w.amount_requested,
                        AuthorFrozenDeltaCoins = -w.amount_requested,
                        StoryId = null,
                        ChapterId = null,
                        StoryTitle = string.Empty,
                        ChapterTitle = string.Empty,
                        AdminId = w.processed_by,
                        BuyerUserId = null,
                        AuthorUserId = w.author_id,
                        Note = w.admin_note
                    };
                // Optional narrowing if user picked only one of APPROVED/REJECTED.
                if (!acceptAll)
                {
                    if (t == "WITHDRAW_APPROVED") parts.Add(withdrawProcessedQ.Where(x => x.EventType == "WITHDRAW_APPROVED"));
                    else if (t == "WITHDRAW_REJECTED") parts.Add(withdrawProcessedQ.Where(x => x.EventType == "WITHDRAW_REJECTED"));
                    else parts.Add(withdrawProcessedQ);
                }
                else parts.Add(withdrawProcessedQ);
            }

            IQueryable<SystemCoinLedgerItemDto> events;
            if (parts.Count == 0)
            {
                events = _db.purchases.AsNoTracking().Where(p => false).Select(p => new SystemCoinLedgerItemDto());
            }
            else
            {
                events = parts[0];
                for (int i = 1; i < parts.Count; i++)
                {
                    events = events.Concat(parts[i]);
                }
            }

            var total = await events.CountAsync(cancellationToken);
            var items = await events
                .OrderByDescending(e => e.EventTime)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new
            {
                totalCount = total,
                page = page,
                pageSize = pageSize,
                items
            });
        }

        private sealed class SystemCoinLedgerItemDto
        {
            public string EventType { get; set; } = null!;
            public DateTime EventTime { get; set; }
            public decimal PlatformDeltaCoins { get; set; }
            public decimal BuyerDeltaCoins { get; set; }
            public decimal AuthorIncomeDeltaCoins { get; set; }
            public decimal AuthorFrozenDeltaCoins { get; set; }
            public Guid? StoryId { get; set; }
            public Guid? ChapterId { get; set; }
            public string StoryTitle { get; set; } = string.Empty;
            public string ChapterTitle { get; set; } = string.Empty;
            public Guid? AdminId { get; set; }
            public Guid? BuyerUserId { get; set; }
            public Guid? AuthorUserId { get; set; }
            public string? Note { get; set; }
        }

        /// <summary>Top tác giả theo thu nhập (coin) dựa trên author_income_logs.</summary>
        [HttpGet("top-authors")]
        public async Task<IActionResult> GetTopAuthors([FromQuery] int take = 10, CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 50);

            var query =
                from log in _db.author_income_logs.AsNoTracking()
                where log.author_id != null
                group log by log.author_id into g
                select new
                {
                    AuthorId = g.Key!.Value,
                    IncomeCoins = g.Sum(x => (decimal?)(x.net_amount ?? x.gross_amount) ?? 0m),
                    Stories = g.Select(x => x.source_id).Where(x => x != null).Distinct().Count()
                };

            var top = await query
                .OrderByDescending(x => x.IncomeCoins)
                .Take(take)
                .ToListAsync(cancellationToken);

            var authorIds = top.Select(x => x.AuthorId).ToList();

            // IMPORTANT: Avoid SQL COALESCE between columns with different collations.
            // We project nickname/email separately then coalesce in-memory.
            var authorNameRows = await (
                from u in _db.users.AsNoTracking()
                join p in _db.user_profiles.AsNoTracking() on u.id equals p.user_id into pj
                from p in pj.DefaultIfEmpty()
                where authorIds.Contains(u.id)
                select new
                {
                    u.id,
                    nickname = p.nickname,
                    email = u.email
                }
            ).ToListAsync(cancellationToken);

            var names = authorNameRows.ToDictionary(
                x => x.id,
                x => string.IsNullOrWhiteSpace(x.nickname) ? (x.email ?? x.id.ToString()) : x.nickname!);

            var items = top.Select((x, idx) => new
            {
                rank = idx + 1,
                id = x.AuthorId,
                name = names.TryGetValue(x.AuthorId, out var n) ? n : x.AuthorId.ToString(),
                incomeCoins = x.IncomeCoins,
                stories = x.Stories
            });

            return Ok(new { items });
        }

        /// <summary>
        /// Top độc giả chi tiêu coin. Hiện tính từ:
        /// - donations (sender_id, amount)
        /// - purchases (user_id, price_paid)
        /// </summary>
        [HttpGet("top-spenders")]
        public async Task<IActionResult> GetTopSpenders([FromQuery] int take = 10, CancellationToken cancellationToken = default)
        {
            take = Math.Clamp(take, 1, 50);

            var donateSpend =
                from d in _db.donations.AsNoTracking()
                where d.sender_id != null
                group d by d.sender_id into g
                select new
                {
                    UserId = g.Key!.Value,
                    Coins = g.Sum(x => (int?)x.amount) ?? 0
                };

            var purchaseSpend =
                from p in _db.purchases.AsNoTracking()
                where p.user_id != null
                group p by p.user_id into g
                select new
                {
                    UserId = g.Key!.Value,
                    Coins = g.Sum(x => (int?)x.price_paid) ?? 0
                };

            var combined =
                from x in donateSpend.Concat(purchaseSpend)
                group x by x.UserId into g
                select new
                {
                    UserId = g.Key,
                    Coins = g.Sum(v => v.Coins)
                };

            var top = await combined
                .OrderByDescending(x => x.Coins)
                .Take(take)
                .ToListAsync(cancellationToken);

            var userIds = top.Select(x => x.UserId).ToList();

            // IMPORTANT: Avoid SQL COALESCE between columns with different collations.
            var userNameRows = await (
                from u in _db.users.AsNoTracking()
                join p in _db.user_profiles.AsNoTracking() on u.id equals p.user_id into pj
                from p in pj.DefaultIfEmpty()
                where userIds.Contains(u.id)
                select new
                {
                    u.id,
                    nickname = p.nickname,
                    email = u.email
                }
            ).ToListAsync(cancellationToken);

            var names = userNameRows.ToDictionary(
                x => x.id,
                x => string.IsNullOrWhiteSpace(x.nickname) ? (x.email ?? x.id.ToString()) : x.nickname!);

            var items = top.Select((x, idx) => new
            {
                rank = idx + 1,
                id = x.UserId,
                name = names.TryGetValue(x.UserId, out var n) ? n : x.UserId.ToString(),
                coins = x.Coins
            });

            return Ok(new { items });
        }
    }
}

