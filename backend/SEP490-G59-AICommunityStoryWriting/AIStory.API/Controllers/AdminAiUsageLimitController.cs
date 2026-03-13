using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIStory.API.Services;

namespace AIStory.API.Controllers;

/// <summary>Admin chỉnh giới hạn số lần sử dụng AI (số lần/24h). Lưu vào bảng ai_configs.</summary>
[ApiController]
[Route("api/admin/ai-usage-limit")]
[Authorize(Roles = "ADMIN")]
public class AdminAiUsageLimitController : ControllerBase
{
    private readonly IAIUsageLimitConfigService _configService;

    public AdminAiUsageLimitController(IAIUsageLimitConfigService configService)
    {
        _configService = configService;
    }

    /// <summary>Xem giới hạn hiện tại (số lần/24h).</summary>
    [HttpGet]
    public IActionResult Get()
    {
        var max = _configService.GetMaxRequestsPerDay();
        return Ok(new { maxRequestsPerDay = max });
    }

    /// <summary>Cập nhật giới hạn (1–100). Có hiệu lực ngay cho các lần gọi AI tiếp theo.</summary>
    [HttpPut]
    public IActionResult Put([FromBody] SetAiUsageLimitRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Body là bắt buộc." });
        if (request.MaxRequestsPerDay < 1 || request.MaxRequestsPerDay > 100)
            return BadRequest(new { message = "maxRequestsPerDay phải từ 1 đến 100." });

        Guid? updatedBy = null;
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (sub != null && Guid.TryParse(sub.Value, out var uid))
            updatedBy = uid;

        _configService.SetMaxRequestsPerDay(request.MaxRequestsPerDay, updatedBy);
        return Ok(new { maxRequestsPerDay = request.MaxRequestsPerDay, message = "Đã cập nhật giới hạn sử dụng AI." });
    }
}

/// <summary>Request cập nhật giới hạn AI.</summary>
public class SetAiUsageLimitRequest
{
    /// <summary>Số lần tối đa sử dụng AI trong 24h (1–100).</summary>
    public int MaxRequestsPerDay { get; set; }
}
