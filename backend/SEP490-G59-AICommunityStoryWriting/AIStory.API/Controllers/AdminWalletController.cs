using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/admin/wallet")]
    [Authorize(Roles = "ADMIN")]
    public sealed class AdminWalletController : ControllerBase
    {
        private readonly StoryPlatformDbContext _db;

        public AdminWalletController(StoryPlatformDbContext db)
        {
            _db = db;
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

