using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using StoryPlatform.DTOs.Account;

[Authorize]
[ApiController]
[Route("api/account")]
public class AccountController : ControllerBase
{
    private readonly IAccountService _service;

    public AccountController(IAccountService service)
    {
        _service = service;
    }

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")!;

    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        return Ok(await _service.GetProfileAsync(UserId));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> Update(UpdateProfileRequest request)
    {
        await _service.UpdateProfileAsync(UserId, request);
        return Ok("Updated");
    }

    [HttpPut("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        await _service.ChangePasswordAsync(UserId, request);
        return Ok("Password changed");
    }
}
