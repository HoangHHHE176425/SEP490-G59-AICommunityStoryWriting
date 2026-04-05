using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Payments;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/coins")]
    public class CoinsController : ControllerBase
    {
        private readonly ICoinPaymentService _coinPaymentService;
        private readonly ILogger<CoinsController> _logger;
        private readonly StoryPlatformDbContext _db;

        public CoinsController(ICoinPaymentService coinPaymentService, ILogger<CoinsController> logger, StoryPlatformDbContext db)
        {
            _coinPaymentService = coinPaymentService;
            _logger = logger;
            _db = db;
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                throw new Exception("Invalid Token or User ID format");
            return userId;
        }

        private const int BANK_ACCOUNT_NUMBER_MIN = 6;
        private const int BANK_ACCOUNT_NUMBER_MAX = 19;
        private const int BANK_ACCOUNT_HOLDER_MIN = 3;
        private const int BANK_ACCOUNT_HOLDER_MAX = 100;
        private const int BANK_BRANCH_MAX = 120;

        private static string CollapseSpaces(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            return Regex.Replace(input.Trim(), @"\s+", " ");
        }

        private static bool TryNormalizeAuthorBankAccountInput(
            AuthorBankAccountUpsertRequest request,
            out string bankName,
            out string accountNumberDigits,
            out string accountHolderUpper,
            out string? branchName,
            out string errorMessage)
        {
            bankName = string.Empty;
            accountNumberDigits = string.Empty;
            accountHolderUpper = string.Empty;
            branchName = null;
            errorMessage = string.Empty;

            if (request == null)
            {
                errorMessage = "Payload không hợp lệ.";
                return false;
            }

            bankName = (request.BankName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(bankName))
            {
                errorMessage = "Vui lòng chọn ngân hàng.";
                return false;
            }
            if (bankName.Length > 100)
            {
                errorMessage = "Tên ngân hàng tối đa 100 ký tự.";
                return false;
            }

            var accountRaw = (request.AccountNumber ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(accountRaw))
            {
                errorMessage = "Vui lòng nhập số tài khoản (chỉ chữ số).";
                return false;
            }
            if (!Regex.IsMatch(accountRaw, @"^\d+$"))
            {
                errorMessage = "Số tài khoản chỉ được chứa chữ số.";
                return false;
            }
            accountNumberDigits = accountRaw;
            if (accountNumberDigits.Length < BANK_ACCOUNT_NUMBER_MIN || accountNumberDigits.Length > BANK_ACCOUNT_NUMBER_MAX)
            {
                errorMessage = $"Số tài khoản cần từ {BANK_ACCOUNT_NUMBER_MIN} đến {BANK_ACCOUNT_NUMBER_MAX} chữ số.";
                return false;
            }

            var holderNorm = CollapseSpaces(request.AccountHolderName ?? string.Empty);
            if (string.IsNullOrWhiteSpace(holderNorm))
            {
                errorMessage = "Vui lòng nhập tên chủ tài khoản.";
                return false;
            }
            if (holderNorm.Length < BANK_ACCOUNT_HOLDER_MIN)
            {
                errorMessage = $"Tên chủ tài khoản tối thiểu {BANK_ACCOUNT_HOLDER_MIN} ký tự.";
                return false;
            }
            if (holderNorm.Length > BANK_ACCOUNT_HOLDER_MAX)
            {
                errorMessage = $"Tên chủ tài khoản tối đa {BANK_ACCOUNT_HOLDER_MAX} ký tự.";
                return false;
            }
            if (!Regex.IsMatch(holderNorm, @"^[\p{L}\s'.-]+$", RegexOptions.Compiled))
            {
                errorMessage = "Tên chủ tài khoản chỉ dùng chữ cái (có dấu), khoảng trắng và các ký tự . ' -";
                return false;
            }
            accountHolderUpper = holderNorm.ToUpper(new CultureInfo("vi-VN"));

            var branchTrim = (request.BranchName ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(branchTrim))
            {
                if (branchTrim.Length > BANK_BRANCH_MAX)
                {
                    errorMessage = $"Chi nhánh tối đa {BANK_BRANCH_MAX} ký tự.";
                    return false;
                }
                if (branchTrim.Length > 255)
                {
                    // safety vs DB constraint (StoryPlatformDbContext config)
                    errorMessage = "Chi nhánh quá dài.";
                    return false;
                }
                branchName = branchTrim;
            }

            return true;
        }

        /// <summary>Danh sách gói coin (active)</summary>
        [HttpGet("packages")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPackages(CancellationToken cancellationToken)
        {
            try
            {
                var list = await _coinPaymentService.GetActivePackagesAsync(cancellationToken);
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPackages failed");
                return StatusCode(500, new { message = ex.Message });
            }
        }

        /// <summary>Lấy ví coin của tôi</summary>
        [HttpGet("wallet")]
        [Authorize]
        public async Task<IActionResult> GetMyWallet(CancellationToken cancellationToken)
        {
            var userId = GetUserIdFromToken();
            var wallet = await _coinPaymentService.GetOrCreateWalletAsync(userId, cancellationToken);
            return Ok(wallet);
        }

        /// <summary>
        /// Lịch sử mở khóa chương trả phí của user (lấy từ bảng purchases, purchase_type = CHAPTER_UNLOCK).
        /// Dùng để hiển thị “lịch sử trừ tiền” ở màn hình ví.
        /// </summary>
        [HttpGet("wallet/unlock-history")]
        [Authorize]
        public async Task<IActionResult> GetMyChapterUnlockHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] int? minCoins = null,
            [FromQuery] int? maxCoins = null,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var userId = GetUserIdFromToken();
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var hasSearch = search != null;

            await using var db = new StoryPlatformDbContext();

            var query = (
                from p in db.purchases.AsNoTracking()
                let unlockedAt = p.released_at ?? p.created_at
                where p.user_id == userId
                    && p.purchase_type == "CHAPTER_UNLOCK"
                    && p.chapter_id != null
                    && p.story_id != null
                    && (dateFrom == null || unlockedAt >= dateFrom)
                    && (dateTo == null || unlockedAt <= dateTo)
                    && (minCoins == null || p.price_paid >= minCoins.Value)
                    && (maxCoins == null || p.price_paid <= maxCoins.Value)
                join c in db.chapters.AsNoTracking() on p.chapter_id equals c.id
                join s in db.stories.AsNoTracking() on p.story_id equals s.id
                where !hasSearch || (
                    (s.title != null && s.title.Contains(search!)) ||
                    (c.title != null && c.title.Contains(search!))
                )
                orderby unlockedAt descending
                select new ChapterUnlockHistoryItemDto
                {
                    PurchaseId = p.id,
                    StoryId = s.id,
                    StoryTitle = s.title ?? string.Empty,
                    ChapterId = c.id,
                    ChapterTitle = c.title ?? string.Empty,
                    CoinsPaid = p.price_paid,
                    UnlockedAt = unlockedAt ?? DateTime.UtcNow
                }
            );

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new ChapterUnlockHistoryResponseDto
            {
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                Items = items
            });
        }

        /// <summary>
        /// Lịch sử donate của user (bảng donations, sender_id = user).
        /// Dùng để hiển thị “lịch sử trừ tiền” ở màn hình ví (tab Ví của tôi > Lịch sử).
        /// </summary>
        [HttpGet("wallet/donate-history")]
        [Authorize]
        public async Task<IActionResult> GetMyDonateHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var userId = GetUserIdFromToken();

            await using var db = new StoryPlatformDbContext();

            var query =
                (
                    from d in db.donations.AsNoTracking()
                    where d.sender_id == userId
                    join s in db.stories.AsNoTracking() on d.story_id equals s.id into sj
                    from s in sj.DefaultIfEmpty() // donations hiện tại có thể không gắn story (story_id = null)
                    orderby (d.created_at ?? DateTime.UtcNow) descending
                    select new WalletDonateHistoryItemDto
                    {
                        DonationId = d.id,
                        StoryId = s != null ? s.id : Guid.Empty,
                        StoryTitle = s != null ? (s.title ?? string.Empty) : string.Empty,
                        CoinsPaid = d.amount,
                        DonatedAt = d.created_at ?? DateTime.UtcNow
                    }
                );

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new WalletDonateHistoryResponseDto
            {
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                Items = items
            });
        }

        /// <summary>
        /// Lịch sử thu nhập của AUTHOR từ việc unlock chapter trả phí.
        /// Nguồn: author_income_logs.source_type = "CHAPTER_UNLOCK"
        /// </summary>
        [HttpGet("author/unlock-chapter-income-history")]
        [Authorize(Roles = "AUTHOR")]
        public async Task<IActionResult> GetAuthorChapterUnlockIncomeHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var userId = GetUserIdFromToken();

            await using var db = new StoryPlatformDbContext();

            var baseQuery = (
                from log in db.author_income_logs.AsNoTracking()
                where log.author_id == userId && log.source_type == "CHAPTER_UNLOCK"
                join p in db.purchases.AsNoTracking() on log.source_id equals p.id
                join c in db.chapters.AsNoTracking() on p.chapter_id equals c.id
                join s in db.stories.AsNoTracking() on p.story_id equals s.id
                let unlockedAt = p.released_at ?? p.created_at
                orderby unlockedAt descending
                select new AuthorChapterUnlockIncomeHistoryItemDto
                {
                    PurchaseId = p.id,
                    StoryId = s.id,
                    StoryTitle = s.title ?? string.Empty,
                    ChapterId = c.id,
                    ChapterTitle = c.title ?? string.Empty,
                    CoinsPaid = p.price_paid,
                    GrossAmount = log.gross_amount ?? 0m,
                    PlatformFee = log.platform_fee ?? 0m,
                    NetAmount = log.net_amount ?? 0m,
                    UnlockedAt = unlockedAt ?? DateTime.UtcNow
                }
            );

            var total = await baseQuery.CountAsync(cancellationToken);
            var items = await baseQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new AuthorChapterUnlockIncomeHistoryResponseDto
            {
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                Items = items
            });
        }

        /// <summary>
        /// Lịch sử thu nhập (AUTHOR) gom theo từng story từ unlock chapter trả phí.
        /// Hỗ trợ filter: search theo tên story, lọc theo tháng và status của author_income_logs.
        /// </summary>
        [HttpGet("author/unlock-chapter-income-history/by-story")]
        [Authorize(Roles = "AUTHOR")]
        public async Task<IActionResult> GetAuthorChapterUnlockIncomeHistoryByStory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? monthFrom = null, // format: yyyy-MM
            [FromQuery] string? monthTo = null,   // format: yyyy-MM
            [FromQuery] string? status = null,     // e.g. AVAILABLE, ALL
            CancellationToken cancellationToken = default)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);

            var userId = GetUserIdFromToken();
            search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
            var hasSearch = search != null;

            status = string.IsNullOrWhiteSpace(status) ? null : status.Trim();
            var hasStatusFilter = status != null && !string.Equals(status, "ALL", StringComparison.OrdinalIgnoreCase);

            DateTime? dateFromUtc = null;
            DateTime? dateToExclusiveUtc = null;

            (DateTime start, DateTime endExclusive)? ParseMonthRange(string? m)
            {
                if (string.IsNullOrWhiteSpace(m)) return null;
                if (!DateTime.TryParseExact(m.Trim(), "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt))
                    return null;
                var start = new DateTime(dt.Year, dt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                var endExclusive = start.AddMonths(1);
                return (start, endExclusive);
            }

            var rangeFrom = ParseMonthRange(monthFrom);
            var rangeTo = ParseMonthRange(monthTo);

            if (rangeFrom != null) dateFromUtc = rangeFrom.Value.start;
            if (rangeTo != null) dateToExclusiveUtc = rangeTo.Value.endExclusive;

            if (dateFromUtc != null && dateToExclusiveUtc == null && rangeFrom != null)
                dateToExclusiveUtc = rangeFrom.Value.endExclusive;

            if (dateFromUtc == null && dateToExclusiveUtc != null && rangeTo != null)
                dateFromUtc = rangeTo.Value.start;

            await using var db = new StoryPlatformDbContext();

            var baseQuery = (
                from log in db.author_income_logs.AsNoTracking()
                where log.author_id == userId
                    && log.source_type == "CHAPTER_UNLOCK"
                join p in db.purchases.AsNoTracking() on log.source_id equals p.id
                join c in db.chapters.AsNoTracking() on p.chapter_id equals c.id
                join s in db.stories.AsNoTracking() on p.story_id equals s.id
                let unlockedAt = p.released_at ?? p.created_at
                where (!hasStatusFilter || ((log.status ?? string.Empty) == status))
                    && (dateFromUtc == null || unlockedAt >= dateFromUtc)
                    && (dateToExclusiveUtc == null || unlockedAt < dateToExclusiveUtc)
                    && (!hasSearch || (s.title != null && s.title.Contains(search!)))
                select new
                {
                    StoryId = s.id,
                    StoryTitle = s.title ?? string.Empty,
                    log.gross_amount,
                    log.platform_fee,
                    log.net_amount,
                    log.status,
                    unlockedAt
                }
            );

            var groupedQuery = (
                from x in baseQuery
                group x by new { x.StoryId, x.StoryTitle } into g
                select new AuthorChapterUnlockIncomeByStoryItemDto
                {
                    StoryId = g.Key.StoryId,
                    StoryTitle = g.Key.StoryTitle,
                    GrossAmount = g.Sum(z => z.gross_amount ?? 0m),
                    PlatformFee = g.Sum(z => z.platform_fee ?? 0m),
                    NetAmount = g.Sum(z => z.net_amount ?? 0m),
                    UnlockCount = g.Count(),
                    LastUnlockedAt = g.Max(z => z.unlockedAt)
                }
            );

            var orderedQuery = groupedQuery.OrderByDescending(x => x.LastUnlockedAt);

            var total = await orderedQuery.CountAsync(cancellationToken);
            var items = await orderedQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(cancellationToken);

            return Ok(new AuthorChapterUnlockIncomeByStoryResponseDto
            {
                TotalCount = total,
                Page = page,
                PageSize = pageSize,
                Items = items
            });
        }

        /// <summary>Danh sách đơn nạp coin của tôi (mặc định 50)</summary>
        [HttpGet("orders")]
        [Authorize]
        public async Task<IActionResult> GetMyOrders([FromQuery] int take = 50, CancellationToken cancellationToken = default)
        {
            var userId = GetUserIdFromToken();
            var list = await _coinPaymentService.GetMyOrdersAsync(userId, take, cancellationToken);
            return Ok(list);
        }

        /// <summary>Đồng bộ trạng thái đơn PayOS (PENDING -> PAID/CANCELLED/EXPIRED/FAILED)</summary>
        [HttpPost("orders/{orderId:guid}/sync")]
        [Authorize]
        public async Task<IActionResult> SyncMyOrder(Guid orderId, CancellationToken cancellationToken)
        {
            try
            {
                var userId = GetUserIdFromToken();
                var updated = await _coinPaymentService.SyncMyPayOSOrderAsync(userId, orderId, cancellationToken);
                return Ok(updated);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Tạo link thanh toán PayOS cho gói coin</summary>
        [HttpPost("payos/create")]
        [Authorize]
        public async Task<IActionResult> CreatePayOS([FromBody] CreatePayOSPaymentRequestDto request, CancellationToken cancellationToken)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            try
            {
                var userId = GetUserIdFromToken();
                var result = await _coinPaymentService.CreatePayOSPaymentAsync(userId, request, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Health-check endpoint để PayOS xác nhận webhook URL</summary>
        [HttpGet("payos/webhook")]
        [AllowAnonymous]
        public IActionResult PayOSWebhookHealthCheck()
        {
            return Ok(new { message = "OK" });
        }

        /// <summary>Webhook PayOS (PayOS gọi vào) - verify signature + cộng coin</summary>
        [HttpPost("payos/webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> PayOSWebhook(CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(Request.Body);
            var raw = await reader.ReadToEndAsync(cancellationToken);

            try
            {
                if (string.IsNullOrWhiteSpace(raw))
                {
                    // Some webhook verifiers may POST with empty body.
                    return Ok(new { message = "OK" });
                }
                var status = await _coinPaymentService.ProcessPayOSWebhookAsync(raw, cancellationToken);
                return Ok(new { message = "Webhook processed", status });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "PayOS webhook failed");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Ủng hộ coin cho tác giả (chuyển coin giữa 2 ví + ghi log donations)</summary>
        [HttpPost("donate")]
        [Authorize]
        public async Task<IActionResult> Donate([FromBody] DonateRequestDto request, CancellationToken cancellationToken)
        {
            if (request == null) return BadRequest(new { message = "Payload không hợp lệ." });
            if (request.Amount <= 0) return BadRequest(new { message = "Số coin ủng hộ phải lớn hơn 0." });
            if (request.AuthorId == Guid.Empty) return BadRequest(new { message = "AuthorId là bắt buộc." });

            try
            {
                var senderId = GetUserIdFromToken();
                var result = await _coinPaymentService.DonateAsync(senderId, request.AuthorId, request.Amount, request.Message, cancellationToken);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Donate failed");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Lịch sử donate nhận + rút tiền của tác giả (user hiện tại). Dùng cho trang author.</summary>
        [HttpGet("author/activity")]
        [Authorize]
        public async Task<IActionResult> GetAuthorActivity([FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken cancellationToken = default)
        {
            var userId = GetUserIdFromToken();
            var result = await _coinPaymentService.GetAuthorActivityAsync(userId, page, pageSize, cancellationToken);
            return Ok(result);
        }

        /// <summary>Tạo yêu cầu rút tiền (tác giả). Trừ coin từ ví khi tạo; admin xử lý duyệt/chuyển tiền sau.</summary>
        [HttpPost("author/withdraw-request")]
        [Authorize]
        public async Task<IActionResult> CreateWithdrawRequest([FromBody] CreateWithdrawRequestDto request, CancellationToken cancellationToken = default)
        {
            if (request == null || request.AmountCoins <= 0)
                return BadRequest(new { message = "Số coin rút phải lớn hơn 0." });

            try
            {
                var userId = GetUserIdFromToken();
                var result = await _coinPaymentService.CreateWithdrawRequestAsync(userId, request.AmountCoins, request.BankInfo, cancellationToken);
                return Ok(result);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CreateWithdrawRequest failed");
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Tác giả hủy yêu cầu rút tiền khi đang chờ admin xử lý.</summary>
        [HttpPost("author/withdraw/{id:guid}/cancel")]
        [Authorize]
        public async Task<IActionResult> CancelWithdrawRequest(Guid id, CancellationToken cancellationToken = default)
        {
            try
            {
                var userId = GetUserIdFromToken();
                await _coinPaymentService.CancelWithdrawRequestAsync(userId, id, cancellationToken);
                return Ok(new { success = true });
            }
            catch (ArgumentOutOfRangeException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "CancelWithdrawRequest failed");
                return BadRequest(new { message = ex.Message });
            }
        }

        // ========= Author bank accounts (for payout to author) =========
        public sealed class AuthorBankAccountUpsertRequest
        {
            public string BankName { get; set; } = null!;
            public string? BankBin { get; set; } // PayOS toBin (not persisted in current DB schema)
            public string AccountNumber { get; set; } = null!;
            public string AccountHolderName { get; set; } = null!;
            public string? BranchName { get; set; }
            public bool IsVerified { get; set; }
        }

        public sealed class AuthorBankAccountVerifyRequest
        {
            public bool IsVerified { get; set; }
        }

        /// <summary>Lấy tài khoản ngân hàng đã lưu của author</summary>
        [HttpGet("author/bank-accounts")]
        [Authorize]
        public async Task<IActionResult> GetAuthorBankAccount(CancellationToken cancellationToken = default)
        {
            var userId = GetUserIdFromToken();

            var acc = await _db.author_bank_accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.user_id == userId, cancellationToken);

            if (acc == null)
                return Ok(new { items = Array.Empty<object>() });

            return Ok(new
            {
                items = new[]
                {
                    new
                    {
                        user_id = acc.user_id,
                        bank_name = acc.bank_name,
                        bank_bin = (string?)null,
                        account_number = acc.account_number,
                        account_holder_name = acc.account_holder_name,
                        branch_name = acc.branch_name,
                        is_verified = acc.is_verified ?? false,
                        updated_at = acc.updated_at
                    }
                }
            });
        }

        /// <summary>Thêm/cập nhật tài khoản ngân hàng của author (upsert theo user_id)</summary>
        [HttpPost("author/bank-accounts")]
        [Authorize]
        public async Task<IActionResult> UpsertAuthorBankAccount([FromBody] AuthorBankAccountUpsertRequest request, CancellationToken cancellationToken = default)
        {
            if (!TryNormalizeAuthorBankAccountInput(
                    request,
                    out var bankName,
                    out var accountNumberDigits,
                    out var accountHolderUpper,
                    out var branchName,
                    out var errorMessage))
            {
                return BadRequest(new { message = errorMessage });
            }

            var userId = GetUserIdFromToken();

            // NOTE: author_bank_accounts currently has columns:
            // bank_name, account_number, account_holder_name, branch_name, is_verified
            var acc = await _db.author_bank_accounts.FirstOrDefaultAsync(x => x.user_id == userId, cancellationToken);
            if (acc == null)
            {
                acc = new BusinessObjects.Entities.author_bank_accounts
                {
                    user_id = userId,
                    bank_name = bankName,
                    account_number = accountNumberDigits,
                    account_holder_name = accountHolderUpper,
                    branch_name = branchName,
                    is_verified = request.IsVerified,
                    updated_at = DateTime.UtcNow
                };
                _db.author_bank_accounts.Add(acc);
            }
            else
            {
                acc.bank_name = bankName;
                acc.account_number = accountNumberDigits;
                acc.account_holder_name = accountHolderUpper;
                acc.branch_name = branchName;
                acc.is_verified = request.IsVerified;
                acc.updated_at = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }

        /// <summary>Toggle verified cho tài khoản ngân hàng của author</summary>
        [HttpPost("author/bank-accounts/verify")]
        [Authorize]
        public async Task<IActionResult> VerifyAuthorBankAccount([FromBody] AuthorBankAccountVerifyRequest request, CancellationToken cancellationToken = default)
        {
            if (request == null) return BadRequest(new { message = "Payload không hợp lệ." });
            var userId = GetUserIdFromToken();

            var acc = await _db.author_bank_accounts.FirstOrDefaultAsync(x => x.user_id == userId, cancellationToken);
            if (acc == null) return NotFound(new { message = "Tài khoản ngân hàng chưa tồn tại." });

            acc.is_verified = request.IsVerified;
            acc.updated_at = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }

        /// <summary>Xóa tài khoản ngân hàng của author</summary>
        [HttpDelete("author/bank-accounts")]
        [Authorize]
        public async Task<IActionResult> DeleteAuthorBankAccount(CancellationToken cancellationToken = default)
        {
            var userId = GetUserIdFromToken();

            var acc = await _db.author_bank_accounts.FirstOrDefaultAsync(x => x.user_id == userId, cancellationToken);
            if (acc == null) return Ok(new { success = true });

            _db.author_bank_accounts.Remove(acc);
            await _db.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true });
        }
    }
}

