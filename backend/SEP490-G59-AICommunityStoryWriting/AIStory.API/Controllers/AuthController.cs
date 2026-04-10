using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Auth; // Đảm bảo namespace chứa VerifyOtpRequest
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Text.Json;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.WebUtilities;

namespace AIStory.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;

        public AuthController(IAuthService authService, IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _authService = authService;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                await _authService.RegisterAsync(request);
                // SỬA MESSAGE: Báo người dùng check mail thay vì bảo login ngay
                return Ok(new { message = "Registration successful. Please check your email for OTP verification." });
            }
            catch (Exception ex)
            {
                // Log inner exception for debugging
                var errorMessage = ex.Message;
                if (ex.InnerException != null)
                {
                    errorMessage += $" Inner: {ex.InnerException.Message}";
                }
                return BadRequest(new { message = errorMessage });
            }
        }

        // THÊM API MỚI: Xác thực OTP
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                await _authService.VerifyAccountAsync(request);
                return Ok(new { message = "Account verified successfully. You can now login." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// Resend OTP xác thực email (EMAIL_VERIFICATION).
        /// </summary>
        [HttpPost("resend-otp")]
        public async Task<IActionResult> ResendOtp([FromBody] ResendOtpRequest request)
        {
            try
            {
                var res = await _authService.ResendOtpAsync(request);
                return Ok(res);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);

                // Store refresh token in HttpOnly cookie (professional approach)
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    SetRefreshTokenCookie(response.RefreshToken);
                }

                // Return access token only (refresh in cookie)
                return Ok(new AccessTokenResponse { AccessToken = response.AccessToken });
            }
            catch (Exception ex)
            {

                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Unauthorized(new { message = "Missing refresh token." });
                }

                var response = await _authService.RefreshAsync(refreshToken);
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    SetRefreshTokenCookie(response.RefreshToken);
                }

                return Ok(new AccessTokenResponse { AccessToken = response.AccessToken });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await _authService.LogoutAsync(refreshToken);
                }

                DeleteRefreshTokenCookie();
                return Ok(new { message = "Logged out." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _authService.ForgotPasswordAsync(request);

                return Ok(new { message = "Nếu email tồn tại, mã OTP đã được gửi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            // Dev: allow running over plain HTTP on localhost:5000.
            // If request is HTTP, using SameSite=None + Secure=true will prevent the cookie from being set.
            var isHttps = Request.IsHttps;
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                Path = "/"
            };

            Response.Cookies.Append("refreshToken", refreshToken, options);
        }

        private void DeleteRefreshTokenCookie()
        {
            var isHttps = Request.IsHttps;
            var options = new CookieOptions
            {
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Path = "/"
            };
            Response.Cookies.Delete("refreshToken", options);
        }

        private void SetCookie(string name, string value, int days, bool httpOnly = true)
        {
            var isHttps = Request.IsHttps;
            var options = new CookieOptions
            {
                HttpOnly = httpOnly,
                Secure = isHttps,
                SameSite = isHttps ? SameSiteMode.None : SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddDays(days),
                Path = "/"
            };
            Response.Cookies.Append(name, value, options);
        }

        private string? GetCookie(string name)
            => Request.Cookies.TryGetValue(name, out var v) ? v : null;

        private IActionResult RedirectGoogleCallbackResult(string frontendOrigin, string returnUrl, string? accessToken = null, string? error = null)
        {
            Response.Cookies.Delete("google_oauth_state");
            Response.Cookies.Delete("google_oauth_returnUrl");

            var feCallback = $"{frontendOrigin}/auth/google/callback";
            var qs = new Dictionary<string, string?>
            {
                ["returnUrl"] = returnUrl
            };

            if (!string.IsNullOrWhiteSpace(accessToken))
            {
                qs["accessToken"] = accessToken;
            }

            if (!string.IsNullOrWhiteSpace(error))
            {
                qs["error"] = error;
            }

            var redirect = QueryHelpers.AddQueryString(feCallback, qs!);
            return Redirect(redirect);
        }

        private static string GetGoogleLoginFriendlyMessage(Exception ex)
        {
            if (string.Equals(ex.Message, "The account has been banned.", StringComparison.OrdinalIgnoreCase))
            {
                return "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên để được hỗ trợ.";
            }

            return "Đăng nhập bằng Google thất bại. Vui lòng thử lại.";
        }

        /// <summary>
        /// Google OAuth redirect code flow: redirect user to Google authorization endpoint.
        /// </summary>
        [HttpGet("google/login")]
        public IActionResult GoogleLogin([FromQuery] string? returnUrl = "/home")
        {
            var clientId = _configuration["GoogleOAuth:ClientId"];
            var clientSecret = _configuration["GoogleOAuth:ClientSecret"];
            var redirectUri = _configuration["GoogleOAuth:RedirectUri"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return BadRequest(new { message = "Missing Google OAuth config (GoogleOAuth:ClientId/ClientSecret)." });

            redirectUri ??= $"{Request.Scheme}://{Request.Host}/api/Auth/google/callback";

            // Only allow internal relative redirects.
            var ru = string.IsNullOrWhiteSpace(returnUrl) ? "/home" : returnUrl.Trim();
            if (!ru.StartsWith("/")) ru = "/home";

            var state = Guid.NewGuid().ToString("N");
            SetCookie("google_oauth_state", state, 1);
            SetCookie("google_oauth_returnUrl", ru, 1);

            var authorizationEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
            var query = new Dictionary<string, string?>
            {
                ["client_id"] = clientId,
                ["redirect_uri"] = redirectUri,
                ["response_type"] = "code",
                ["scope"] = "openid email profile",
                ["access_type"] = "online",
                ["prompt"] = "select_account",
                ["state"] = state
            };

            var googleUrl = QueryHelpers.AddQueryString(authorizationEndpoint, query!);
            return Redirect(googleUrl);
        }

        /// <summary>
        /// OAuth callback: exchange code -> tokens, validate id_token, then issue our own JWT and redirect back to FE.
        /// </summary>
        [HttpGet("google/callback")]
        public async Task<IActionResult> GoogleCallback(
            [FromQuery] string? code,
            [FromQuery] string? state,
            [FromQuery] string? error)
        {
            var frontendOrigin = _configuration["GoogleOAuth:FrontendOrigin"] ?? "http://localhost:5173";
            var returnUrl = GetCookie("google_oauth_returnUrl") ?? "/home";

            try
            {
                var clientId = _configuration["GoogleOAuth:ClientId"];
                var clientSecret = _configuration["GoogleOAuth:ClientSecret"];
                var redirectUri = _configuration["GoogleOAuth:RedirectUri"];

                if (!string.IsNullOrWhiteSpace(error))
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Đăng nhập Google đã bị hủy hoặc gặp lỗi.");

                if (string.IsNullOrWhiteSpace(code))
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Không nhận được mã xác thực từ Google.");
                if (string.IsNullOrWhiteSpace(state))
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Phiên đăng nhập Google không hợp lệ. Vui lòng thử lại.");

                if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Hệ thống đăng nhập Google đang tạm thời không khả dụng.");

                redirectUri ??= $"{Request.Scheme}://{Request.Host}/api/Auth/google/callback";

                var expectedState = GetCookie("google_oauth_state");
                if (expectedState == null || !string.Equals(expectedState, state, StringComparison.Ordinal))
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Phiên đăng nhập Google đã hết hạn hoặc không hợp lệ.");

                var tokenEndpoint = "https://oauth2.googleapis.com/token";
                var client = _httpClientFactory.CreateClient();

                var form = new Dictionary<string, string?>
                {
                    ["code"] = code,
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["redirect_uri"] = redirectUri,
                    ["grant_type"] = "authorization_code"
                };

                using var tokenResp = await client.PostAsync(
                    tokenEndpoint,
                    new FormUrlEncodedContent(form!),
                    HttpContext.RequestAborted);

                var body = await tokenResp.Content.ReadAsStringAsync();
                if (!tokenResp.IsSuccessStatusCode)
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Không thể xác thực với Google. Vui lòng thử lại.");

                using var doc = JsonDocument.Parse(body);
                var idToken = doc.RootElement.TryGetProperty("id_token", out var idTokenEl) ? idTokenEl.GetString() : null;
                if (string.IsNullOrWhiteSpace(idToken))
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Không nhận được thông tin tài khoản từ Google.");

                // Validate id_token signature + audience.
                var certsJson = await client.GetStringAsync("https://www.googleapis.com/oauth2/v3/certs");
                var jwks = new JsonWebKeySet(certsJson);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateAudience = true,
                    ValidAudience = clientId,
                    ValidateIssuer = true,
                    ValidIssuers = new[] { "https://accounts.google.com", "accounts.google.com" },
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKeys = jwks.Keys,
                    ClockSkew = TimeSpan.FromMinutes(2),
                };

                var handler = new JwtSecurityTokenHandler();
                handler.InboundClaimTypeMap.Clear();

                var principal = handler.ValidateToken(idToken, validationParameters, out _);

                var email = principal.FindFirst("email")?.Value;
                var name = principal.FindFirst("name")?.Value;
                var sub = principal.FindFirst("sub")?.Value;

                if (string.IsNullOrWhiteSpace(email))
                    return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: "Không lấy được email từ tài khoản Google.");

                var auth = await _authService.LoginWithGoogleAsync(email, name, sub ?? string.Empty);
                if (!string.IsNullOrWhiteSpace(auth.RefreshToken))
                    SetRefreshTokenCookie(auth.RefreshToken);

                return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, accessToken: auth.AccessToken);
            }
            catch (Exception ex)
            {
                return RedirectGoogleCallbackResult(frontendOrigin, returnUrl, error: GetGoogleLoginFriendlyMessage(ex));
            }
        }

    }
}