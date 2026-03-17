using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Payments;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/coins")]
    public class CoinsController : ControllerBase
    {
        private readonly ICoinPaymentService _coinPaymentService;
        private readonly ILogger<CoinsController> _logger;

        public CoinsController(ICoinPaymentService coinPaymentService, ILogger<CoinsController> logger)
        {
            _coinPaymentService = coinPaymentService;
            _logger = logger;
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                throw new Exception("Invalid Token or User ID format");
            return userId;
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
    }
}

