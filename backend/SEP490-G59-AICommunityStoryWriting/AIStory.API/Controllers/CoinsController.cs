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
            var list = await _coinPaymentService.GetActivePackagesAsync(cancellationToken);
            return Ok(list);
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
    }
}

