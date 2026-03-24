using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Services.Integrations.PayOS;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/admin/transactions")]
    [Authorize(Roles = "ADMIN")]
    public sealed class AdminTransactionsController : ControllerBase
    {
        private readonly StoryPlatformDbContext _db;
        private readonly PayOSClient _payos;

        // Fixed conversion rate: 100 coin = 10,000 VND => 1 coin = 100 VND
        private const decimal CoinRateVnd = 100m;

        public AdminTransactionsController(StoryPlatformDbContext db, PayOSClient payos)
        {
            _db = db;
            _payos = payos;
        }

        private static string? Extract(string? snapshot, string key)
        {
            if (string.IsNullOrWhiteSpace(snapshot) || string.IsNullOrWhiteSpace(key)) return null;
            // FE snapshot format: key=value | key2=value2 | ...
            var parts = snapshot.Split('|', StringSplitOptions.RemoveEmptyEntries);
            foreach (var raw in parts)
            {
                var part = raw.Trim();
                var kv = part.Split('=', 2, StringSplitOptions.RemoveEmptyEntries);
                if (kv.Length != 2) continue;
                var k = kv[0].Trim();
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                    return kv[1].Trim();
            }
            return null;
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
                        // Backward compatibility: some old records may still use "SUCCESS".
                        "SUCCESS" => withdrawQ.Where(x => (x.status ?? "PENDING") == "SUCCESS" || (x.status ?? "PENDING") == "COMPLETED"),
                        "COMPLETED" => withdrawQ.Where(x => (x.status ?? "PENDING") == "COMPLETED"),
                        "PROCESSING" => withdrawQ.Where(x => (x.status ?? "PENDING") == "PROCESSING"),
                        "PENDING" => withdrawQ.Where(x => (x.status ?? "PENDING") == "PENDING"),
                        "PENDING_REVIEW" => withdrawQ.Where(x => (x.status ?? "PENDING") == "PENDING_REVIEW"),
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
                        bankAccount = bankAccount,
                        fraudReview = new
                        {
                            isSuspectedFraud = w.is_suspected_fraud ?? false,
                            riskScore = w.risk_score,
                            riskFlags = w.risk_flags,
                            riskReason = w.risk_reason,
                            reviewedBy = w.reviewed_by,
                            reviewedAt = w.reviewed_at
                        }
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

        /// <summary>
        /// Duyệt yêu cầu rút (withdraw_requests):
        /// - PENDING/PENDING_REVIEW -> PROCESSING
        /// - Tạo PayOS payout batch và sync trạng thái về COMPLETED/FAILED trong background.
        /// </summary>
        [HttpPost("withdraw/{id:guid}/approve")]
        public async Task<IActionResult> ApproveWithdraw(Guid id, [FromBody] AdminWithdrawDecisionRequest? body, CancellationToken cancellationToken)
        {
            var adminId = GetCurrentAdminId();
            var req = await _db.withdraw_requests.FirstOrDefaultAsync(x => x.id == id, cancellationToken);
            if (req == null) return NotFound(new { message = "Withdraw request not found." });
            var s = (req.status ?? "PENDING").ToUpperInvariant();
            if (s != "PENDING" && s != "PENDING_REVIEW")
                return BadRequest(new { message = "Only PENDING/PENDING_REVIEW withdraw requests can be approved." });
            if (s == "PENDING_REVIEW" && string.IsNullOrWhiteSpace(body?.AdminNote))
                return BadRequest(new { message = "Admin note is required when approving a PENDING_REVIEW withdraw request." });

            if (req.author_id == null)
                return BadRequest(new { message = "Withdraw request missing author_id." });

            string DigitsOnly(string? s)
            {
                if (string.IsNullOrWhiteSpace(s)) return string.Empty;
                var sb = new System.Text.StringBuilder(s.Length);
                foreach (var ch in s)
                {
                    if (char.IsDigit(ch)) sb.Append(ch);
                }
                return sb.ToString();
            }

            var bankBin = DigitsOnly(Extract(req.bank_info_snapshot, "bank_bin"));
            var toAccountNumber = DigitsOnly(Extract(req.bank_info_snapshot, "account_number"));

            if (string.IsNullOrWhiteSpace(bankBin) || string.IsNullOrWhiteSpace(toAccountNumber))
            {
                return BadRequest(new
                {
                    message = "Missing bank_bin/account_number in withdraw request. FE must provide toBin for PayOS payout."
                });
            }

            // Convert coins (income_balance) -> VND and create payout.
            var amountVnd = req.amount_requested * CoinRateVnd;
            var amountVndInt = (long)Math.Round(amountVnd, 0, MidpointRounding.AwayFromZero);

            // Create payout batch to route transfer to author.
            var payoutReferenceId = $"wd_{req.id}";
            var payoutItemReferenceId = $"wd_{req.id}_1";

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            IActionResult result = StatusCode(500, new { message = "Unexpected error." });

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

                try
                {
                    // Quick auth check: payouts-account/balance doesn't require x-signature.
                    // If this fails (e.g. IP blocked), we still continue so we can see the real error from CreatePayoutBatchAsync.
                    string? balanceCheckError = null;
                    try
                    {
                        var balanceCheck = await _payos.GetPayoutAccountBalanceAsync(cancellationToken);
                        if (!string.Equals(balanceCheck.Code, "00", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException($"PayOS payout auth check failed: code={balanceCheck.Code}, desc={balanceCheck.Desc}");
                    }
                    catch (Exception exBal)
                    {
                        balanceCheckError = exBal.Message;
                    }

                    var payoutRes = await _payos.CreatePayoutBatchAsync(new PayOSClient.PayoutBatchRequest(
                        ReferenceId: payoutReferenceId,
                        Category: new List<string> { "salary" },
                        ValidateDestination: true,
                        Payouts: new List<PayOSClient.PayoutBatchItem>
                        {
                            new PayOSClient.PayoutBatchItem(
                                ReferenceId: payoutItemReferenceId,
                                Amount: amountVndInt,
                                // Avoid non-ASCII characters in signature payload to prevent "invalid signature" issues.
                                Description: "Rut tien",
                                ToBin: bankBin.Trim(),
                                ToAccountNumber: toAccountNumber.Trim()
                            )
                        }
                    ), idempotencyKey: payoutReferenceId, cancellationToken: cancellationToken);

                    var immediateMappedStatus = MapPayoutToWithdrawStatus(payoutRes.ApprovalState, null);
                    req.status = immediateMappedStatus ?? "PROCESSING";
                    req.processed_at = DateTime.UtcNow;
                    req.processed_by = adminId;
                    req.reviewed_by = adminId;
                    req.reviewed_at = DateTime.UtcNow;
                    if (!string.IsNullOrWhiteSpace(body?.AdminNote)) req.admin_note = body!.AdminNote!.Trim();
                    if (!string.IsNullOrWhiteSpace(balanceCheckError))
                    {
                        var warn = $"PayOS auth check warn: {balanceCheckError}";
                        if (string.IsNullOrWhiteSpace(req.admin_note))
                            req.admin_note = warn;
                        else
                            req.admin_note = $"{req.admin_note} | {warn}";

                        if (req.admin_note.Length > 500) req.admin_note = req.admin_note.Substring(0, 500);
                    }

                    // Store PayOS payout id for later/manual sync.
                    req.transaction_proof_url = payoutRes.PayoutId;

                    // If PayOS already returns a terminal state, settle wallet immediately.
                    if (req.author_id.HasValue && immediateMappedStatus is "COMPLETED" or "FAILED")
                    {
                        var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == req.author_id.Value, cancellationToken);
                        if (wallet != null)
                        {
                            wallet.frozen_balance = Math.Max(0m, (wallet.frozen_balance ?? 0m) - req.amount_requested);
                            if (immediateMappedStatus == "FAILED")
                            {
                                wallet.income_balance = (wallet.income_balance ?? 0m) + req.amount_requested;
                            }
                            wallet.updated_at = DateTime.UtcNow;
                        }
                    }

                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    result = Ok(new { success = true, payoutId = payoutRes.PayoutId });
                }
                catch (Exception ex)
                {
                    // If payout creation fails, rollback wallet by refunding frozen -> income_balance.
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

                    req.status = "FAILED";
                    req.processed_at = DateTime.UtcNow;
                    req.processed_by = adminId;
                    // Always append PayOS error details so you can see why transaction_proof_url stayed empty.
                    var baseNote = body?.AdminNote?.Trim();
                    var payosErr = ex.Message ?? string.Empty;
                    var combinedNote = string.IsNullOrWhiteSpace(baseNote)
                        ? $"PayOS payout failed: {payosErr}"
                        : $"{baseNote} | PayOS payout failed: {payosErr}";
                    if (combinedNote.Length > 500) combinedNote = combinedNote.Substring(0, 500);
                    req.admin_note = combinedNote;
                    _db.withdraw_requests.Update(req);
                    await _db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    result = BadRequest(new { message = $"Create PayOS payout failed: {ex.Message}" });
                }
            });

            return result;
        }

        /// <summary>Từ chối yêu cầu rút (withdraw_requests) - chuyển PENDING/PENDING_REVIEW -> FAILED.</summary>
        [HttpPost("withdraw/{id:guid}/reject")]
        public async Task<IActionResult> RejectWithdraw(Guid id, [FromBody] AdminWithdrawDecisionRequest? body, CancellationToken cancellationToken)
        {
            var adminId = GetCurrentAdminId();
            var req = await _db.withdraw_requests.FirstOrDefaultAsync(x => x.id == id, cancellationToken);
            if (req == null) return NotFound(new { message = "Withdraw request not found." });
            var s = (req.status ?? "PENDING").ToUpperInvariant();
            if (s != "PENDING" && s != "PENDING_REVIEW")
                return BadRequest(new { message = "Only PENDING/PENDING_REVIEW withdraw requests can be rejected." });
            if (s == "PENDING_REVIEW" && string.IsNullOrWhiteSpace(body?.AdminNote))
                return BadRequest(new { message = "Admin note is required when rejecting a PENDING_REVIEW withdraw request." });

            var executionStrategy = _db.Database.CreateExecutionStrategy();
            IActionResult result = StatusCode(500, new { message = "Unexpected error." });

            await executionStrategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

                req.status = "FAILED";
                req.processed_at = DateTime.UtcNow;
                req.processed_by = adminId;
                req.reviewed_by = adminId;
                req.reviewed_at = DateTime.UtcNow;
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
                result = Ok(new { success = true });
            });

            return result;
        }

        /// <summary>
        /// Đồng bộ thủ công trạng thái 1 withdraw từ PayOS theo payoutId (transaction_proof_url).
        /// Dùng khi PayOS đã COMPLETED nhưng DB local vẫn PROCESSING.
        /// </summary>
        [HttpPost("withdraw/{id:guid}/sync")]
        public async Task<IActionResult> SyncWithdrawStatus(Guid id, CancellationToken cancellationToken)
        {
            var req = await _db.withdraw_requests.FirstOrDefaultAsync(x => x.id == id, cancellationToken);
            if (req == null) return NotFound(new { message = "Withdraw request not found." });

            if (string.IsNullOrWhiteSpace(req.transaction_proof_url))
                return BadRequest(new { message = "Withdraw request does not have payoutId (transaction_proof_url)." });

            var payout = await _payos.GetPayoutInfoAsync(req.transaction_proof_url!, cancellationToken);
            var mappedStatus = MapPayoutToWithdrawStatus(payout.ApprovalState, payout.FirstTransactionState);

            if (mappedStatus == null)
            {
                return Ok(new
                {
                    success = true,
                    message = "Payout is not terminal yet.",
                    approvalState = payout.ApprovalState,
                    currentStatus = req.status
                });
            }

            // If already terminal, keep idempotent.
            var currentStatus = (req.status ?? "").Trim().ToUpperInvariant();
            if (currentStatus == "COMPLETED" || currentStatus == "FAILED" || currentStatus == "CANCELLED")
            {
                return Ok(new
                {
                    success = true,
                    message = "Withdraw already terminal.",
                    approvalState = payout.ApprovalState,
                    currentStatus = req.status
                });
            }

            var strategy = _db.Database.CreateExecutionStrategy();
            IActionResult result = StatusCode(500, new { message = "Unexpected error." });

            await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);

                var current = await _db.withdraw_requests.FirstOrDefaultAsync(x => x.id == id, cancellationToken);
                if (current == null)
                {
                    result = NotFound(new { message = "Withdraw request not found." });
                    return;
                }

                var oldStatus = current.status;
                var now = DateTime.UtcNow;
                current.processed_at = now;

                if (mappedStatus == "COMPLETED")
                {
                    if (current.author_id.HasValue)
                    {
                        var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == current.author_id.Value, cancellationToken);
                        if (wallet != null)
                        {
                            wallet.frozen_balance = Math.Max(0m, (wallet.frozen_balance ?? 0m) - current.amount_requested);
                            wallet.updated_at = now;
                        }
                    }
                    current.status = "COMPLETED";
                }
                else
                {
                    if (current.author_id.HasValue)
                    {
                        var wallet = await _db.wallets.FirstOrDefaultAsync(w => w.user_id == current.author_id.Value, cancellationToken);
                        if (wallet != null)
                        {
                            wallet.frozen_balance = Math.Max(0m, (wallet.frozen_balance ?? 0m) - current.amount_requested);
                            wallet.income_balance = (wallet.income_balance ?? 0m) + current.amount_requested;
                            wallet.updated_at = now;
                        }
                    }
                    current.status = "FAILED";
                }

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);

                result = Ok(new
                {
                    success = true,
                    oldStatus,
                    newStatus = current.status,
                    approvalState = payout.ApprovalState,
                    payoutId = current.transaction_proof_url
                });
            });

            return result;
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
                // Backward compatibility.
                "SUCCESS" => "COMPLETED",
                "COMPLETED" => "COMPLETED",
                "PENDING" => "PENDING",
                "PENDING_REVIEW" => "PENDING_REVIEW",
                "PROCESSING" => "PROCESSING",
                "CANCELLED" => "CANCELLED",
                "FAILED" => "FAILED",
                _ => s
            };
        }

        private static string? MapPayoutToWithdrawStatus(string? approvalState, string? firstTransactionState)
        {
            var txState = (firstTransactionState ?? string.Empty).Trim().ToUpperInvariant();
            if (txState is "SUCCEEDED" or "COMPLETED") return "COMPLETED";
            if (txState is "FAILED" or "CANCELLED" or "REVERSED") return "FAILED";

            var approval = (approvalState ?? string.Empty).Trim().ToUpperInvariant();
            if (approval is "SUCCEEDED" or "COMPLETED" or "PARTIAL_COMPLETED") return "COMPLETED";
            if (approval is "FAILED" or "REJECTED" or "CANCELLED") return "FAILED";
            return null;
        }
    }
}

