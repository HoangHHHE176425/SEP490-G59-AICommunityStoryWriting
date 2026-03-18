using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/admin/transactions")]
    [Authorize(Roles = "ADMIN")]
    public sealed class AdminTransactionsController : ControllerBase
    {
        private readonly StoryPlatformDbContext _db;

        // Fixed conversion rate: 100 coin = 10,000 VND => 1 coin = 100 VND
        private const decimal CoinRateVnd = 100m;

        public AdminTransactionsController(StoryPlatformDbContext db)
        {
            _db = db;
        }

        private Guid? GetCurrentAdminId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        public sealed class AdminTransactionListResponse
        {
            public List<object> Items { get; set; } = new();
            public int TotalCount { get; set; }
            public int Page { get; set; }
            public int PageSize { get; set; }
            public int TotalPages { get; set; }
        }

        /// <summary>
        /// Danh sách giao dịch admin (DEPOSIT từ coin_orders + WITHDRAW từ withdraw_requests).
        /// Params:
        /// - type: ALL|DEPOSIT|WITHDRAW
        /// - status: ALL|SUCCESS|PENDING|FAILED|CANCELLED
        /// - q: search by id/email/nickname/gatewayRef
        /// - from,to: yyyy-MM-dd
        /// - page,pageSize
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetTransactions(
            [FromQuery] string type = "ALL",
            [FromQuery] string status = "ALL",
            [FromQuery] string q = "",
            [FromQuery] string? from = null,
            [FromQuery] string? to = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 5, 100);

            var typeUpper = (type ?? "ALL").Trim().ToUpperInvariant();
            var statusUpper = (status ?? "ALL").Trim().ToUpperInvariant();
            var query = (q ?? "").Trim();

            DateTime? fromUtc = null;
            if (!string.IsNullOrWhiteSpace(from) && DateTime.TryParse(from, out var fromDate))
                fromUtc = DateTime.SpecifyKind(fromDate.Date, DateTimeKind.Utc);

            DateTime? toUtc = null;
            if (!string.IsNullOrWhiteSpace(to) && DateTime.TryParse(to, out var toDate))
                toUtc = DateTime.SpecifyKind(toDate.Date.AddDays(1).AddTicks(-1), DateTimeKind.Utc);

            var items = new List<(DateTime createdAt, object dto)>();

            // ---- DEPOSIT (coin_orders)
            if (typeUpper is "ALL" or "DEPOSIT")
            {
                var depositsQ = _db.coin_orders.AsNoTracking();

                if (fromUtc.HasValue) depositsQ = depositsQ.Where(x => x.created_at != null && x.created_at >= fromUtc);
                if (toUtc.HasValue) depositsQ = depositsQ.Where(x => x.created_at != null && x.created_at <= toUtc);

                if (statusUpper != "ALL")
                {
                    // Map coin_orders.status -> admin status
                    depositsQ = statusUpper switch
                    {
                        "SUCCESS" => depositsQ.Where(x => (x.status ?? "PENDING") == "PAID"),
                        "PENDING" => depositsQ.Where(x => (x.status ?? "PENDING") == "PENDING"),
                        "FAILED" => depositsQ.Where(x => (x.status ?? "PENDING") == "FAILED"),
                        "CANCELLED" => depositsQ.Where(x => (x.status ?? "PENDING") == "CANCELLED" || (x.status ?? "PENDING") == "EXPIRED"),
                        _ => depositsQ
                    };
                }

                // Preload user display names to avoid collation issues in SQL.
                // We'll filter q in memory if q is non-empty.
                var deposits = await depositsQ
                    .OrderByDescending(x => x.created_at)
                    .Take(500) // safety bound for admin list
                    .ToListAsync(cancellationToken);

                var depositUserIds = deposits.Select(x => x.user_id).Distinct().ToList();
                var depositNameRows = await (
                    from u in _db.users.AsNoTracking()
                    join p in _db.user_profiles.AsNoTracking() on u.id equals p.user_id into pj
                    from p in pj.DefaultIfEmpty()
                    where depositUserIds.Contains(u.id)
                    select new { u.id, nickname = p.nickname, email = u.email }
                ).ToListAsync(cancellationToken);
                var depositNames = depositNameRows.ToDictionary(
                    x => x.id,
                    x => string.IsNullOrWhiteSpace(x.nickname) ? (x.email ?? x.id.ToString()) : x.nickname!);

                foreach (var d in deposits)
                {
                    var createdAt = d.created_at ?? d.completed_at ?? DateTime.UtcNow;
                    var statusMapped = MapDepositStatus(d.status);
                    var gatewayRef = d.gateway_transaction_id ?? d.id.ToString();
                    var userName = depositNames.TryGetValue(d.user_id, out var dn) ? dn : d.user_id.ToString();

                    var dto = new
                    {
                        id = d.id.ToString(),
                        createdAt = createdAt,
                        user = new { id = d.user_id.ToString(), name = userName, email = depositNameRows.FirstOrDefault(x => x.id == d.user_id)?.email ?? "" },
                        type = "DEPOSIT",
                        amountVnd = d.amount_paid,
                        method = d.payment_gateway ?? "PAYOS",
                        status = statusMapped,
                        note = "Nạp coin",
                        gatewayRef = gatewayRef,
                    };

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var hay = $"{dto.id} {userName} {dto.user.email} {dto.gatewayRef} {dto.method} {dto.status}".ToLowerInvariant();
                        if (!hay.Contains(query.ToLowerInvariant())) continue;
                    }

                    items.Add((createdAt, dto));
                }
            }

            // ---- WITHDRAW (withdraw_requests)
            if (typeUpper is "ALL" or "WITHDRAW")
            {
                var withdrawQ = _db.withdraw_requests.AsNoTracking();
                if (fromUtc.HasValue) withdrawQ = withdrawQ.Where(x => x.created_at != null && x.created_at >= fromUtc);
                if (toUtc.HasValue) withdrawQ = withdrawQ.Where(x => x.created_at != null && x.created_at <= toUtc);

                if (statusUpper != "ALL")
                {
                    withdrawQ = statusUpper switch
                    {
                        "SUCCESS" => withdrawQ.Where(x => (x.status ?? "PENDING") == "SUCCESS"),
                        "PENDING" => withdrawQ.Where(x => (x.status ?? "PENDING") == "PENDING"),
                        "FAILED" => withdrawQ.Where(x => (x.status ?? "PENDING") == "FAILED"),
                        "CANCELLED" => withdrawQ.Where(x => (x.status ?? "PENDING") == "CANCELLED"),
                        _ => withdrawQ
                    };
                }

                var withdraws = await withdrawQ
                    .OrderByDescending(x => x.created_at)
                    .Take(500)
                    .ToListAsync(cancellationToken);

                var withdrawUserIds = withdraws.Where(x => x.author_id.HasValue).Select(x => x.author_id!.Value).Distinct().ToList();
                var withdrawNameRows = await (
                    from u in _db.users.AsNoTracking()
                    join p in _db.user_profiles.AsNoTracking() on u.id equals p.user_id into pj
                    from p in pj.DefaultIfEmpty()
                    where withdrawUserIds.Contains(u.id)
                    select new { u.id, nickname = p.nickname, email = u.email }
                ).ToListAsync(cancellationToken);
                var withdrawNames = withdrawNameRows.ToDictionary(
                    x => x.id,
                    x => string.IsNullOrWhiteSpace(x.nickname) ? (x.email ?? x.id.ToString()) : x.nickname!);

                foreach (var w in withdraws)
                {
                    var createdAt = w.created_at ?? DateTime.UtcNow;
                    var authorId = w.author_id ?? Guid.Empty;
                    var userName = authorId != Guid.Empty && withdrawNames.TryGetValue(authorId, out var wn) ? wn : authorId.ToString();
                    var userEmail = withdrawNameRows.FirstOrDefault(x => x.id == authorId)?.email ?? "";

                    // bank_info_snapshot is JSON-ish string written by FE. Best-effort parse.
                    object? bankAccount = null;
                    if (!string.IsNullOrWhiteSpace(w.bank_info_snapshot))
                    {
                        try
                        {
                            var doc = JsonDocument.Parse(w.bank_info_snapshot);
                            bankAccount = doc.RootElement.Clone();
                        }
                        catch
                        {
                            bankAccount = null;
                        }
                    }

                    var statusMapped = MapWithdrawStatus(w.status);
                    var amountCoins = (int)Math.Round(w.amount_requested);
                    var amountVnd = w.amount_requested * CoinRateVnd;

                    var dto = new
                    {
                        id = w.id.ToString(),
                        createdAt = createdAt,
                        user = new { id = authorId.ToString(), name = userName, email = userEmail },
                        type = "WITHDRAW",
                        amountVnd = amountVnd,
                        amountCoins = amountCoins,
                        method = "BankTransfer",
                        status = statusMapped,
                        note = "Rút về ngân hàng",
                        gatewayRef = $"WD-{w.id.ToString()[..8]}",
                        bankAccount = bankAccount
                    };

                    if (!string.IsNullOrWhiteSpace(query))
                    {
                        var hay = $"{dto.id} {userName} {dto.user.email} {dto.gatewayRef} {dto.method} {dto.status}".ToLowerInvariant();
                        if (!hay.Contains(query.ToLowerInvariant())) continue;
                    }

                    items.Add((createdAt, dto));
                }
            }

            var ordered = items.OrderByDescending(x => x.createdAt).Select(x => x.dto).ToList();
            var totalCount = ordered.Count;
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            var paged = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new AdminTransactionListResponse
            {
                Items = paged,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages
            });
        }

        /// <summary>Duyệt yêu cầu rút (withdraw_requests) - chuyển PENDING -> SUCCESS.</summary>
        [HttpPost("withdraw/{id:guid}/approve")]
        public async Task<IActionResult> ApproveWithdraw(Guid id, [FromBody] AdminWithdrawDecisionRequest? body, CancellationToken cancellationToken)
        {
            var adminId = GetCurrentAdminId();
            var req = await _db.withdraw_requests.FirstOrDefaultAsync(x => x.id == id, cancellationToken);
            if (req == null) return NotFound(new { message = "Withdraw request not found." });
            if (!string.Equals(req.status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Only PENDING withdraw requests can be approved." });

            using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            req.status = "SUCCESS";
            req.processed_at = DateTime.UtcNow;
            req.processed_by = adminId;
            if (!string.IsNullOrWhiteSpace(body?.AdminNote)) req.admin_note = body!.AdminNote!.Trim();

            // Funds were moved to frozen_balance when the request was created.
            // On approve, we release frozen funds (completed payout off-platform).
            if (req.author_id.HasValue)
            {
                var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == req.author_id.Value, cancellationToken);
                if (wallet != null)
                {
                    wallet.frozen_balance = Math.Max(0m, (wallet.frozen_balance ?? 0m) - req.amount_requested);
                    wallet.updated_at = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Ok(new { success = true });
        }

        /// <summary>Từ chối yêu cầu rút (withdraw_requests) - chuyển PENDING -> CANCELLED.</summary>
        [HttpPost("withdraw/{id:guid}/reject")]
        public async Task<IActionResult> RejectWithdraw(Guid id, [FromBody] AdminWithdrawDecisionRequest? body, CancellationToken cancellationToken)
        {
            var adminId = GetCurrentAdminId();
            var req = await _db.withdraw_requests.FirstOrDefaultAsync(x => x.id == id, cancellationToken);
            if (req == null) return NotFound(new { message = "Withdraw request not found." });
            if (!string.Equals(req.status, "PENDING", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "Only PENDING withdraw requests can be rejected." });

            using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

            req.status = "CANCELLED";
            req.processed_at = DateTime.UtcNow;
            req.processed_by = adminId;
            if (!string.IsNullOrWhiteSpace(body?.AdminNote)) req.admin_note = body!.AdminNote!.Trim();

            // Refund: frozen -> income_balance (withdrawable again).
            if (req.author_id.HasValue)
            {
                var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == req.author_id.Value, cancellationToken);
                if (wallet != null)
                {
                    wallet.frozen_balance = Math.Max(0m, (wallet.frozen_balance ?? 0m) - req.amount_requested);
                    wallet.income_balance = (wallet.income_balance ?? 0m) + req.amount_requested;
                    wallet.updated_at = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Ok(new { success = true });
        }

        public sealed class AdminWithdrawDecisionRequest
        {
            public string? AdminNote { get; set; }
        }

        private static string MapDepositStatus(string? status)
        {
            var s = (status ?? "PENDING").ToUpperInvariant();
            return s switch
            {
                "PAID" => "SUCCESS",
                "PENDING" => "PENDING",
                "CANCELLED" => "CANCELLED",
                "EXPIRED" => "CANCELLED",
                "FAILED" => "FAILED",
                _ => "FAILED"
            };
        }

        private static string MapWithdrawStatus(string? status)
        {
            var s = (status ?? "PENDING").ToUpperInvariant();
            return s switch
            {
                "SUCCESS" => "SUCCESS",
                "PENDING" => "PENDING",
                "CANCELLED" => "CANCELLED",
                "FAILED" => "FAILED",
                _ => s
            };
        }
    }
}

