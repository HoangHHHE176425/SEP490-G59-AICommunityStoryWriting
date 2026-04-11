using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Auth; // Đảm bảo namespace chứa VerifyOtpRequest
using Services.Interfaces;
using System.Collections.Generic;
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

        /// <summary>
        /// redirect_uri gửi Google phải cùng host với request (cookie OAuth). Nếu config trỏ localhost
        /// nhưng user mở API qua tunnel — bắt buộc dùng URL từ request.
        /// </summary>
        private string ResolveGoogleOAuthRedirectUri()
        {
            var fromRequest = $"{Request.Scheme}://{Request.Host}/api/Auth/google/callback";
            var configured = _configuration["GoogleOAuth:RedirectUri"]?.Trim();
            if (string.IsNullOrWhiteSpace(configured))
                return fromRequest;

            if (!Uri.TryCreate(configured, UriKind.Absolute, out var cfgUri))
                return fromRequest;

            if (!string.Equals(cfgUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase))
                return fromRequest;

            if (!string.Equals(cfgUri.Scheme, Request.Scheme, StringComparison.OrdinalIgnoreCase))
                return fromRequest;

            return configured;
        }

        private bool IsAllowedFrontendOrigin(string? origin)
        {
            if (string.IsNullOrWhiteSpace(origin)) return false;
            var o = origin.Trim().TrimEnd('/');
            if (!Uri.TryCreate(o, UriKind.Absolute, out var u) || (u.Scheme != Uri.UriSchemeHttp && u.Scheme != Uri.UriSchemeHttps))
                return false;
            if (!string.IsNullOrEmpty(u.AbsolutePath) && u.AbsolutePath != "/")
                return false;

            var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var primary = _configuration["GoogleOAuth:FrontendOrigin"]?.Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(primary)) allowed.Add(primary);
            foreach (var x in _configuration.GetSection("GoogleOAuth:AllowedFrontendOrigins").Get<string[]>() ?? System.Array.Empty<string>())
            {
                if (!string.IsNullOrWhiteSpace(x)) allowed.Add(x.Trim().TrimEnd('/'));
            }

            return allowed.Contains(o);
        }

        private string GetOAuthFrontendBase()
        {
            var c = GetCookie("google_oauth_fe_origin");
            if (!string.IsNullOrEmpty(c) && IsAllowedFrontendOrigin(c.Trim()))
                return c.Trim().TrimEnd('/');
            return (_configuration["GoogleOAuth:FrontendOrigin"] ?? "http://localhost:5173").Trim().TrimEnd('/');
        }

        /// <summary>Redirect về FE /auth/google/callback với mã lỗi (tránh trang JSON BadRequest).</summary>
        private IActionResult RedirectGoogleOAuthError(string errorCode, string? returnUrlFallback = null)
        {
            var fe = GetOAuthFrontendBase();
            var ru = returnUrlFallback;
            if (string.IsNullOrWhiteSpace(ru)) ru = GetCookie("google_oauth_returnUrl");
            if (string.IsNullOrWhiteSpace(ru) || !ru.Trim().StartsWith("/")) ru = "/home";
            else ru = ru.Trim();
            var target = $"{fe}/auth/google/callback";
            var q = new Dictionary<string, string?> { ["error"] = errorCode, ["returnUrl"] = ru };
            return Redirect(QueryHelpers.AddQueryString(target, q!));
        }

        /// <summary>
        /// Google OAuth redirect code flow: redirect user to Google authorization endpoint.
        /// </summary>
        [HttpGet("google/login")]
        public IActionResult GoogleLogin([FromQuery] string? returnUrl = "/home", [FromQuery] string? feOrigin = null)
        {
            var clientId = _configuration["GoogleOAuth:ClientId"];
            var clientSecret = _configuration["GoogleOAuth:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return BadRequest(new { message = "Missing Google OAuth config (GoogleOAuth:ClientId/ClientSecret)." });

            var redirectUri = ResolveGoogleOAuthRedirectUri();

            // Only allow internal relative redirects.
            var ru = string.IsNullOrWhiteSpace(returnUrl) ? "/home" : returnUrl.Trim();
            if (!ru.StartsWith("/")) ru = "/home";

            var feBase = IsAllowedFrontendOrigin(feOrigin)
                ? feOrigin!.Trim().TrimEnd('/')
                : (_configuration["GoogleOAuth:FrontendOrigin"] ?? "http://localhost:5173").Trim().TrimEnd('/');

            var state = Guid.NewGuid().ToString("N");
            SetCookie("google_oauth_state", state, 1);
            SetCookie("google_oauth_returnUrl", ru, 1);
            SetCookie("google_oauth_fe_origin", feBase, 1);

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
            var clientId = _configuration["GoogleOAuth:ClientId"];
            var clientSecret = _configuration["GoogleOAuth:ClientSecret"];
            var redirectUri = ResolveGoogleOAuthRedirectUri();

            if (!string.IsNullOrWhiteSpace(error))
            {
                var errCode = string.Equals(error, "access_denied", StringComparison.OrdinalIgnoreCase)
                    ? "google_access_denied"
                    : "google_auth_error";
                return RedirectGoogleOAuthError(errCode);
            }

            if (string.IsNullOrWhiteSpace(code))
                return RedirectGoogleOAuthError("oauth_missing_code");
            if (string.IsNullOrWhiteSpace(state))
                return RedirectGoogleOAuthError("oauth_missing_state");

            if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
                return BadRequest(new { message = "Missing Google OAuth config (GoogleOAuth:ClientId/ClientSecret)." });

            var expectedState = GetCookie("google_oauth_state");
            if (expectedState == null || !string.Equals(expectedState, state, StringComparison.Ordinal))
                return RedirectGoogleOAuthError("oauth_state_invalid");

            var returnUrl = GetCookie("google_oauth_returnUrl") ?? "/home";

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
                return RedirectGoogleOAuthError("oauth_exchange_failed");

            using var doc = JsonDocument.Parse(body);
            var idToken = doc.RootElement.TryGetProperty("id_token", out var idTokenEl) ? idTokenEl.GetString() : null;
            if (string.IsNullOrWhiteSpace(idToken))
                return RedirectGoogleOAuthError("google_no_id_token");

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

            System.Security.Claims.ClaimsPrincipal principal;
            try
            {
                principal = handler.ValidateToken(idToken, validationParameters, out _);
            }
            catch (SecurityTokenException)
            {
                return RedirectGoogleOAuthError("google_id_token_invalid");
            }

            var email = principal.FindFirst("email")?.Value;
            var name = principal.FindFirst("name")?.Value;
            var sub = principal.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(email))
                return RedirectGoogleOAuthError("google_no_email");

            AuthResponse auth;
            try
            {
                auth = await _authService.LoginWithGoogleAsync(email, name, sub ?? string.Empty);
            }
            catch
            {
                return RedirectGoogleOAuthError("google_login_failed");
            }
            if (!string.IsNullOrWhiteSpace(auth.RefreshToken))
                SetRefreshTokenCookie(auth.RefreshToken);

            var feBaseSuccess = GetOAuthFrontendBase();

            // Clear state cookies
            Response.Cookies.Delete("google_oauth_state");
            Response.Cookies.Delete("google_oauth_returnUrl");
            Response.Cookies.Delete("google_oauth_fe_origin");

            // Redirect FE callback to store accessToken and finish login.
            var feCallback = $"{feBaseSuccess}/auth/google/callback";
            var qs = new Dictionary<string, string?>
            {
                ["accessToken"] = auth.AccessToken,
                ["returnUrl"] = returnUrl
            };
            var redirect = QueryHelpers.AddQueryString(feCallback, qs!);
            return Redirect(redirect);
        }

    }
}