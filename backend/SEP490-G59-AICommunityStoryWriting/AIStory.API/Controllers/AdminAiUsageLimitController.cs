using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using AIStory.API.Services;

namespace AIStory.API.Controllers;

/// <summary>Admin chỉnh giới hạn số lần sử dụng AI (số lần/24h rolling) theo loại API. Lưu vào bảng ai_configs.</summary>
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

    /// <summary>Xem giới hạn hiện tại (hai loại + field legacy cho FE cũ).</summary>
    [HttpGet]
    public IActionResult Get()
    {
        var suggest = _configService.GetMaxRequestsPerDay(AiRateLimitKind.SuggestNextChapter);
        var coCreate = _configService.GetMaxRequestsPerDay(AiRateLimitKind.CoCreate);
        return Ok(new
        {
            maxRequestsPerDay = suggest,
            maxRequestsPerDaySuggestNextChapter = suggest,
            maxRequestsPerDayCoCreate = coCreate
        });
    }

    /// <summary>Cập nhật giới hạn (1–100). Body legacy: <c>maxRequestsPerDay</c> (áp dụng cho cả hai). Hoặc hai field riêng.</summary>
    [HttpPut]
    public IActionResult Put([FromBody] SetAiUsageLimitRequest request)
    {
        if (request == null)
            return BadRequest(new { message = "Body là bắt buộc." });

        int suggestVal;
        int coCreateVal;

        if (request.MaxRequestsPerDay.HasValue)
        {
            var v = request.MaxRequestsPerDay.Value;
            if (v < 1 || v > 100)
                return BadRequest(new { message = "maxRequestsPerDay phải từ 1 đến 100." });
            suggestVal = coCreateVal = v;
        }
        else
        {
            if (request.MaxRequestsPerDaySuggestNextChapter < 1 || request.MaxRequestsPerDaySuggestNextChapter > 100)
                return BadRequest(new { message = "maxRequestsPerDaySuggestNextChapter phải từ 1 đến 100." });
            if (request.MaxRequestsPerDayCoCreate < 1 || request.MaxRequestsPerDayCoCreate > 100)
                return BadRequest(new { message = "maxRequestsPerDayCoCreate phải từ 1 đến 100." });
            suggestVal = request.MaxRequestsPerDaySuggestNextChapter;
            coCreateVal = request.MaxRequestsPerDayCoCreate;
        }

        Guid? updatedBy = null;
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (sub != null && Guid.TryParse(sub.Value, out var uid))
            updatedBy = uid;

        _configService.SetMaxRequestsPerDay(AiRateLimitKind.SuggestNextChapter, suggestVal, updatedBy);
        _configService.SetMaxRequestsPerDay(AiRateLimitKind.CoCreate, coCreateVal, updatedBy);
        return Ok(new
        {
            maxRequestsPerDay = suggestVal,
            maxRequestsPerDaySuggestNextChapter = suggestVal,
            maxRequestsPerDayCoCreate = coCreateVal,
            message = "Đã cập nhật giới hạn sử dụng AI."
        });
    }
}

/// <summary>Request cập nhật giới hạn AI.</summary>
public class SetAiUsageLimitRequest
{
    /// <summary>Legacy: một giá trị áp dụng cho cả suggest và co-create.</summary>
    public int? MaxRequestsPerDay { get; set; }

    /// <summary>Số lần tối đa POST suggest-next-chapter trong 24h (1–100).</summary>
    public int MaxRequestsPerDaySuggestNextChapter { get; set; }

    /// <summary>Số lần tối đa POST co-create trong 24h (1–100).</summary>
    public int MaxRequestsPerDayCoCreate { get; set; }
}
