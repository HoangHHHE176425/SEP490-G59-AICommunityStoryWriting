using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.DTOs.Admin;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class AdminAiUsageStatsService : IAdminAiUsageStatsService
{
    private const string OpenRouterHost = "openrouter.ai";
    /// <summary>OpenRouter Management API key (sk-or-v1-…) — ưu tiên cho GET /key, /generation, thống kê admin.</summary>
    private const string AiConfigOpenRouterManagementKey = "OpenRouterManagementApiKey";
    /// <summary>Bảng ai_configs: secret OpenRouter dùng cho thống kê /key (sau Management / OpenRouterStatsApiKey).</summary>
    private const string AiConfigOpenRouterStatsKey = "OpenRouterStatsApiKey";
    /// <summary>Bảng ai_configs: alias tùy chọn.</summary>
    private const string AiConfigOpenRouterKeyAlias = "OpenRouterApiKey";
    /// <summary>Một số team lưu literal tên biến môi trường làm key trong ai_configs.</summary>
    private const string AiConfigOpenRouterEnvNameKey = "OPENROUTER_API_KEY";
    private const string AiConfigOpenRouterManagementEnvNameKey = "OPENROUTER_MANAGEMENT_API_KEY";
    private readonly StoryPlatformDbContext _db;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AdminAiUsageStatsService> _logger;

    public AdminAiUsageStatsService(
        StoryPlatformDbContext db,
        IConfiguration configuration,
        ILogger<AdminAiUsageStatsService> logger)
    {
        _db = db;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// HttpClient riêng cho OpenRouter admin (không dùng IHttpClientFactory) để tránh edge-case
    /// handler/pool; gọi thưa nên chi phí tạo client chấp nhận được.
    /// </summary>
    private static HttpClient CreateOpenRouterHttpClient()
    {
        var handler = new SocketsHttpHandler
        {
            AutomaticDecompression = DecompressionMethods.Brotli | DecompressionMethods.GZip | DecompressionMethods.Deflate,
            PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2)
        };
        return new HttpClient(handler, disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(60),
            DefaultRequestVersion = HttpVersion.Version11
        };
    }

    public bool IsOpenRouterConfigured() =>
        UrlContainsOpenRouter(_configuration["AI:BaseUrl"])
        || UrlContainsOpenRouter(_configuration["AI:WritingBaseUrl"])
        || UrlContainsOpenRouter(_configuration["AI:AnalysisBaseUrl"])
        || UrlContainsOpenRouter(_configuration["AI:EmbeddingBaseUrl"]);

    private static bool UrlContainsOpenRouter(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.Contains(OpenRouterHost, StringComparison.OrdinalIgnoreCase);

    /// <summary>OpenRouter GET /key và /generation chỉ chấp nhận secret dạng sk-or-v1-…; key khác (sk-proj-…, Groq, …) vẫn gửi được HTTP nhưng API trả 401 với message gây hiểu nhầm "Missing Authentication header".</summary>
    private static bool IsLikelyOpenRouterSecretKey(string key) =>
        key.StartsWith("sk-or-v1-", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Lấy key gọi OpenRouter /key và /generation. Bỏ qua chuỗi rỗng.
    /// Ưu tiên giá trị có tiền tố sk-or-v1- ở bất kỳ slot nào (tránh chọn AI:ApiKey là Groq/OpenAI trong khi AI:WritingApiKey mới là OpenRouter).
    /// Đọc thêm bảng ai_configs (OpenRouterManagementApiKey / OpenRouterStatsApiKey / …) cho môi trường chỉ lưu secret trong DB.
    /// </summary>
    private async Task<string?> ResolveOpenRouterApiKeyAsync(CancellationToken cancellationToken)
    {
        var dbRows = await _db.ai_configs.AsNoTracking()
            .Where(c =>
                c.key == AiConfigOpenRouterManagementKey
                || c.key == AiConfigOpenRouterManagementEnvNameKey
                || c.key == AiConfigOpenRouterStatsKey
                || c.key == AiConfigOpenRouterKeyAlias
                || c.key == AiConfigOpenRouterEnvNameKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var dbMgmt = dbRows.FirstOrDefault(c => c.key == AiConfigOpenRouterManagementKey)?.value
            ?? dbRows.FirstOrDefault(c => c.key == AiConfigOpenRouterManagementEnvNameKey)?.value;
        var dbStats = dbRows.FirstOrDefault(c => c.key == AiConfigOpenRouterStatsKey)?.value;
        var dbOr = dbRows.FirstOrDefault(c => c.key == AiConfigOpenRouterKeyAlias)?.value;
        var dbEnvNamed = dbRows.FirstOrDefault(c => c.key == AiConfigOpenRouterEnvNameKey)?.value;

        string? firstNonEmpty = null;
        foreach (var raw in EnumerateOpenRouterApiKeyRawCandidates(dbMgmt, dbStats, dbOr, dbEnvNamed))
        {
            var k = NormalizeApiKeyToken(raw);
            if (string.IsNullOrEmpty(k))
                continue;
            var candidate = CoerceOpenRouterKeyFromBlob(k);
            firstNonEmpty ??= candidate;
            if (IsLikelyOpenRouterSecretKey(candidate))
                return candidate;
        }

        return firstNonEmpty;
    }

    /// <summary>
    /// Bỏ ký tự vô hình / format / control đầu-cuối; nếu chuỗi có nhãn hoặc rác phía trước sk-or-v1- thì cắt lấy từ marker (copy từ UI, JSON, .env hay gặp).
    /// </summary>
    private static string CoerceOpenRouterKeyFromBlob(string k)
    {
        var t = StripLeadingTrailingJunk(k);
        if (IsLikelyOpenRouterSecretKey(t))
            return t;
        const string marker = "sk-or-v1-";
        var idx = t.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return t;
        return StripLeadingTrailingJunk(t[idx..]);
    }

    private static bool IsJunkSurroundingKeyChar(char c)
    {
        if (c is '\uFEFF' or '\u200B' or '\u200C' or '\u200D' or '\u2060' or '\u00A0')
            return true;
        if (char.IsControl(c))
            return true;
        return CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.Format;
    }

    private static string StripLeadingTrailingJunk(string t)
    {
        if (string.IsNullOrEmpty(t))
            return t;
        var i = 0;
        var end = t.Length;
        while (i < end && (char.IsWhiteSpace(t[i]) || IsJunkSurroundingKeyChar(t[i])))
            i++;
        while (end > i && (char.IsWhiteSpace(t[end - 1]) || IsJunkSurroundingKeyChar(t[end - 1])))
            end--;
        return i == 0 && end == t.Length ? t : t[i..end];
    }

    /// <summary>Thứ tự ứng viên: Management key → stats → DB → env → key gắn URL OpenRouter → fallback toàn slot.</summary>
    private IEnumerable<string?> EnumerateOpenRouterApiKeyRawCandidates(string? dbMgmt, string? dbStats, string? dbOr, string? dbEnvNamed)
    {
        yield return _configuration["AI:OpenRouterManagementApiKey"];
        yield return _configuration["AI:OpenRouterStatsApiKey"];
        yield return _configuration["AI:OpenRouterApiKey"];
        yield return dbMgmt;
        yield return dbStats;
        yield return dbOr;
        yield return dbEnvNamed;
        yield return Environment.GetEnvironmentVariable("OPENROUTER_MANAGEMENT_API_KEY");
        yield return Environment.GetEnvironmentVariable("OPENROUTER_API_KEY");

        if (UrlContainsOpenRouter(_configuration["AI:WritingBaseUrl"]))
        {
            yield return _configuration["AI:WritingApiKey"];
            yield return _configuration["AI:ApiKey"];
        }

        if (UrlContainsOpenRouter(_configuration["AI:AnalysisBaseUrl"]))
        {
            yield return _configuration["AI:AnalysisApiKey"];
            yield return _configuration["AI:ApiKey"];
        }

        if (UrlContainsOpenRouter(_configuration["AI:EmbeddingBaseUrl"]))
        {
            yield return _configuration["AI:EmbeddingApiKey"];
            yield return _configuration["AI:ApiKey"];
        }

        if (UrlContainsOpenRouter(_configuration["AI:BaseUrl"]))
            yield return _configuration["AI:ApiKey"];

        yield return _configuration["AI:ApiKey"];
        yield return _configuration["AI:WritingApiKey"];
        yield return _configuration["AI:AnalysisApiKey"];
        yield return _configuration["AI:EmbeddingApiKey"];
    }

    /// <summary>Chỉ Management key (GET /api/v1/keys yêu cầu quyền management).</summary>
    private async Task<string?> ResolveOpenRouterManagementApiKeyAsync(CancellationToken cancellationToken)
    {
        var dbRows = await _db.ai_configs.AsNoTracking()
            .Where(c => c.key == AiConfigOpenRouterManagementKey || c.key == AiConfigOpenRouterManagementEnvNameKey)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var dbMgmt = dbRows.FirstOrDefault(c => c.key == AiConfigOpenRouterManagementKey)?.value
            ?? dbRows.FirstOrDefault(c => c.key == AiConfigOpenRouterManagementEnvNameKey)?.value;

        foreach (var raw in new[]
                 {
                     _configuration["AI:OpenRouterManagementApiKey"],
                     dbMgmt,
                     Environment.GetEnvironmentVariable("OPENROUTER_MANAGEMENT_API_KEY")
                 })
        {
            var k = NormalizeApiKeyToken(raw);
            if (string.IsNullOrEmpty(k))
                continue;
            var candidate = CoerceOpenRouterKeyFromBlob(k);
            if (IsLikelyOpenRouterSecretKey(candidate))
                return candidate;
        }

        return null;
    }

    private static string? NormalizeApiKeyToken(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        var t = raw.Trim().TrimStart('\uFEFF').Trim('"', '\'');
        t = t.Replace("\r", "", StringComparison.Ordinal).Replace("\n", "", StringComparison.Ordinal).Replace("\t", "", StringComparison.Ordinal);
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            t = t["Bearer ".Length..].Trim();
        t = t.Trim('"', '\'');
        return string.IsNullOrEmpty(t) ? null : t;
    }

    /// <summary>Gợi ý an toàn (không in secret): vì sao key không được coi là OpenRouter.</summary>
    private static string DescribeRejectedKeyShape(string k)
    {
        if (k.StartsWith("sk-proj", StringComparison.OrdinalIgnoreCase))
            return "đang giống OpenAI (sk-proj-…).";
        if (k.StartsWith("gsk_", StringComparison.OrdinalIgnoreCase))
            return "đang giống Groq (gsk_…).";
        if (k.StartsWith("sk-or", StringComparison.OrdinalIgnoreCase) && !k.StartsWith("sk-or-v1-", StringComparison.OrdinalIgnoreCase))
            return "có sk-or- nhưng không đúng sk-or-v1-… (kiểm tra copy thừa ký tự / sai bản key).";
        if (k.Contains("sk-or-v1-", StringComparison.OrdinalIgnoreCase))
            return "có chuỗi sk-or-v1- nhưng bị ký tự lạ che phía trước — lưu file chỉ còn secret, hoặc dùng AI:OpenRouterManagementApiKey / AI:OpenRouterStatsApiKey.";
        return $"không chứa sk-or-v1- (độ dài {k.Length}).";
    }

    public async Task<(bool Ok, string? Error, OpenRouterKeyStatsDto? Data)> GetOpenRouterKeyStatsAsync(
        CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveOpenRouterApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            return (false,
                "Chưa tìm thấy key OpenRouter: AI:OpenRouterManagementApiKey (Management key, khuyến nghị cho thống kê), AI:OpenRouterStatsApiKey / OpenRouterApiKey, OPENROUTER_API_KEY, ai_configs (OpenRouterManagementApiKey, …), hoặc key đúng với URL có openrouter.ai. Trên server: env AI__OpenRouterManagementApiKey hoặc appsettings.Local.",
                null);
        }

        if (!IsLikelyOpenRouterSecretKey(apiKey))
        {
            return (false,
                $"Không có secret OpenRouter (sk-or-v1-…). Key được chọn {DescribeRejectedKeyShape(apiKey)} Endpoint GET /api/v1/key chỉ chấp nhận key OpenRouter. Thêm Management key vào AI:OpenRouterManagementApiKey (khuyến nghị) hoặc AI:OpenRouterStatsApiKey / OPENROUTER_API_KEY / ai_configs; nếu chat dùng Groq nhưng embedding/writing dùng OpenRouter thì đặt key OpenRouter vào đúng slot (AI:EmbeddingApiKey / AI:WritingApiKey).",
                null);
        }

        try
        {
            using var client = CreateOpenRouterHttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/key");
            req.Version = HttpVersion.Version11;
            if (!TrySetBearerAuthorization(req, apiKey, out var authErr))
                return (false, authErr, null);
            var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                if (resp.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogWarning(
                        "OpenRouter /key 401. Kiểm tra AI:OpenRouterManagementApiKey / OpenRouterStatsApiKey / OPENROUTER_API_KEY (key length {Len}).",
                        apiKey.Length);
                    var hint = body.Contains("Missing Authentication header", StringComparison.OrdinalIgnoreCase)
                        ? " Lưu ý: với OpenRouter, thông báo này thường có nghĩa là key không hợp lệ/không phải sk-or-v1-… hoặc đã thu hồi, không nhất thiết là request thiếu header Authorization."
                        : "";
                    return (false,
                        $"OpenRouter /key HTTP 401: key không hợp lệ hoặc không còn hiệu lực. Kiểm tra AI:OpenRouterManagementApiKey / OpenRouterStatsApiKey, AI:ApiKey/Writing/…, hoặc OPENROUTER_API_KEY.{hint} Chi tiết: {body}",
                        null);
                }

                return (false, $"OpenRouter /key HTTP {(int)resp.StatusCode}: {body}", null);
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("data", out var data))
                return (false, "Phản hồi OpenRouter không có trường data.", null);

            var dto = new OpenRouterKeyStatsDto
            {
                Usage = data.TryGetProperty("usage", out var u) && u.ValueKind == JsonValueKind.Number ? u.GetDouble() : 0,
                UsageDaily = data.TryGetProperty("usage_daily", out var ud) && ud.ValueKind == JsonValueKind.Number ? ud.GetDouble() : 0,
                UsageWeekly = data.TryGetProperty("usage_weekly", out var uw) && uw.ValueKind == JsonValueKind.Number ? uw.GetDouble() : 0,
                UsageMonthly = data.TryGetProperty("usage_monthly", out var um) && um.ValueKind == JsonValueKind.Number ? um.GetDouble() : 0,
                Limit = ReadNullableDouble(data, "limit"),
                LimitRemaining = ReadNullableDouble(data, "limit_remaining"),
                LimitReset = data.TryGetProperty("limit_reset", out var lr) && lr.ValueKind == JsonValueKind.String ? lr.GetString() : null,
                Label = data.TryGetProperty("label", out var lb) && lb.ValueKind == JsonValueKind.String ? lb.GetString() : null,
                IsFreeTier = data.TryGetProperty("is_free_tier", out var ft) && ft.ValueKind == JsonValueKind.True
            };
            return (true, null, dto);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenRouter /key failed");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Ok, string? Error, IReadOnlyList<OpenRouterKeyListItemDto>? Keys)> GetOpenRouterKeysListAsync(
        CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveOpenRouterManagementApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            return (false,
                "Cần OpenRouter Management API key (AI:OpenRouterManagementApiKey, OPENROUTER_MANAGEMENT_API_KEY, hoặc ai_configs OpenRouterManagementApiKey) để gọi GET /api/v1/keys. GET /api/v1/key chỉ mô tả đúng key trong Authorization (management key thường có usage = 0).",
                null);
        }

        try
        {
            using var client = CreateOpenRouterHttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/keys");
            req.Version = HttpVersion.Version11;
            if (!TrySetBearerAuthorization(req, apiKey, out var authErr))
                return (false, authErr, null);
            var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, $"OpenRouter GET /keys HTTP {(int)resp.StatusCode}: {body}", null);

            var keys = ParseOpenRouterKeysListJson(body);
            return (true, null, keys);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenRouter /keys failed");
            return (false, ex.Message, null);
        }
    }

    private static readonly Regex ActivityDateParam = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly Regex ActivityApiKeyHashParam = new(@"^[a-fA-F0-9]{4,128}$", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public async Task<(bool Ok, string? Error, string? Json)> GetOpenRouterActivityRawJsonAsync(
        string? date,
        string? apiKeyHash,
        string? userId,
        CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveOpenRouterManagementApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            return (false,
                "Cần OpenRouter Management API key để gọi GET /api/v1/activity (analytics account-level, giống trang Activity trên web).",
                null);
        }

        if (!string.IsNullOrWhiteSpace(date))
        {
            var d = date.Trim();
            if (!ActivityDateParam.IsMatch(d))
                return (false, "Tham số date phải là YYYY-MM-DD (UTC).", null);
        }

        if (!string.IsNullOrWhiteSpace(apiKeyHash))
        {
            var h = apiKeyHash.Trim();
            if (!ActivityApiKeyHashParam.IsMatch(h))
                return (false, "api_key_hash không hợp lệ (hex, độ dài 4–128).", null);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            var u = userId.Trim();
            if (u.Length is < 1 or > 256 || u.Contains('/', StringComparison.Ordinal) || u.Contains('\\', StringComparison.Ordinal))
                return (false, "user_id không hợp lệ.", null);
        }

        var qs = new StringBuilder();
        void Append(string name, string value)
        {
            if (qs.Length > 0) qs.Append('&');
            qs.Append(name).Append('=').Append(Uri.EscapeDataString(value));
        }

        if (!string.IsNullOrWhiteSpace(date))
            Append("date", date.Trim());
        if (!string.IsNullOrWhiteSpace(apiKeyHash))
            Append("api_key_hash", apiKeyHash.Trim());
        if (!string.IsNullOrWhiteSpace(userId))
            Append("user_id", userId.Trim());

        var url = qs.Length == 0
            ? "https://openrouter.ai/api/v1/activity"
            : "https://openrouter.ai/api/v1/activity?" + qs;

        try
        {
            using var client = CreateOpenRouterHttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Version = HttpVersion.Version11;
            if (!TrySetBearerAuthorization(req, apiKey, out var authErr))
                return (false, authErr, null);
            var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, $"OpenRouter GET /activity HTTP {(int)resp.StatusCode}: {body}", null);
            return (true, null, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenRouter /activity failed");
            return (false, ex.Message, null);
        }
    }

    public async Task<(bool Ok, string? Error, OpenRouterCreditsDto? Data)> GetOpenRouterCreditsAsync(
        CancellationToken cancellationToken = default)
    {
        var apiKey = await ResolveOpenRouterManagementApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            return (false,
                "Cần OpenRouter Management API key để gọi GET /api/v1/credits (credits đã nạp / đã dùng).",
                null);
        }

        try
        {
            using var client = CreateOpenRouterHttpClient();
            using var req = new HttpRequestMessage(HttpMethod.Get, "https://openrouter.ai/api/v1/credits");
            req.Version = HttpVersion.Version11;
            if (!TrySetBearerAuthorization(req, apiKey, out var authErr))
                return (false, authErr, null);
            var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, $"OpenRouter GET /credits HTTP {(int)resp.StatusCode}: {body}", null);

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            var data = root.TryGetProperty("data", out var d) ? d : root;
            var total = ReadOpenRouterCreditsNumber(data, "total_credits", "totalCredits", "total_credit");
            var used = ReadOpenRouterCreditsNumber(data, "total_usage", "totalUsage", "usage");
            var remaining = ReadOpenRouterCreditsNumber(data, "remaining", "remaining_credits", "remainingCredits", "balance");
            if (!remaining.HasValue && total.HasValue && used.HasValue)
                remaining = Math.Max(0, total.Value - used.Value);

            return (true, null, new OpenRouterCreditsDto
            {
                TotalCreditsPurchased = total,
                TotalCreditsUsed = used,
                RemainingCredits = remaining
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenRouter /credits failed");
            return (false, ex.Message, null);
        }
    }

    private static double? ReadOpenRouterCreditsNumber(JsonElement data, params string[] names)
    {
        foreach (var name in names)
        {
            if (!data.TryGetProperty(name, out var p))
                continue;
            switch (p.ValueKind)
            {
                case JsonValueKind.Number:
                    return p.GetDouble();
                case JsonValueKind.String:
                    if (double.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var x))
                        return x;
                    break;
            }
        }

        return null;
    }

    public async Task<(bool Ok, string? Error, string? Json)> GetOpenRouterKeyByHashRawJsonAsync(
        string hash,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hash))
            return (false, "hash bắt buộc.", null);

        var apiKey = await ResolveOpenRouterManagementApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            return (false,
                "Cần OpenRouter Management API key để gọi GET /api/v1/keys/{hash}.",
                null);
        }

        var h = hash.Trim();
        if (h.Contains('/', StringComparison.Ordinal) || h.Contains('\\', StringComparison.Ordinal) || h.Contains("..", StringComparison.Ordinal))
            return (false, "hash không hợp lệ.", null);

        try
        {
            using var client = CreateOpenRouterHttpClient();
            var url = "https://openrouter.ai/api/v1/keys/" + Uri.EscapeDataString(h);
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Version = HttpVersion.Version11;
            if (!TrySetBearerAuthorization(req, apiKey, out var authErr))
                return (false, authErr, null);
            var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, $"OpenRouter GET /keys/{{hash}} HTTP {(int)resp.StatusCode}: {body}", null);
            return (true, null, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenRouter /keys/{{hash}} failed for {Hash}", hash);
            return (false, ex.Message, null);
        }
    }

    private static IReadOnlyList<OpenRouterKeyListItemDto> ParseOpenRouterKeysListJson(string body)
    {
        using var doc = JsonDocument.Parse(body);
        if (!TryGetOpenRouterKeysArray(doc.RootElement, out var arr))
            return Array.Empty<OpenRouterKeyListItemDto>();

        var list = new List<OpenRouterKeyListItemDto>();
        foreach (var el in arr.EnumerateArray())
            list.Add(MapOpenRouterKeyListItem(el));
        return list;
    }

    private static bool TryGetOpenRouterKeysArray(JsonElement root, out JsonElement array)
    {
        array = default;
        if (root.ValueKind == JsonValueKind.Array)
        {
            array = root;
            return true;
        }

        if (!root.TryGetProperty("data", out var data))
            return false;
        if (data.ValueKind == JsonValueKind.Array)
        {
            array = data;
            return true;
        }

        if (data.ValueKind == JsonValueKind.Object)
        {
            if (data.TryGetProperty("data", out var inner) && inner.ValueKind == JsonValueKind.Array)
            {
                array = inner;
                return true;
            }

            if (data.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.Array)
            {
                array = keys;
                return true;
            }
        }

        return false;
    }

    private static OpenRouterKeyListItemDto MapOpenRouterKeyListItem(JsonElement el)
    {
        return new OpenRouterKeyListItemDto
        {
            Hash = ReadJsonString(el, "hash", "id", "key_hash"),
            Label = ReadJsonString(el, "label", "description"),
            Name = ReadJsonString(el, "name"),
            Usage = ReadJsonDouble(el, "usage", 0),
            UsageDaily = ReadJsonDouble(el, "usage_daily", 0),
            UsageWeekly = ReadJsonDouble(el, "usage_weekly", 0),
            UsageMonthly = ReadJsonDouble(el, "usage_monthly", 0),
            Limit = ReadJsonNullableDouble(el, "limit"),
            LimitRemaining = ReadJsonNullableDouble(el, "limit_remaining"),
            LimitReset = ReadJsonString(el, "limit_reset"),
            Disabled = ReadJsonBool(el, "disabled")
        };
    }

    private static string? ReadJsonString(JsonElement el, params string[] names)
    {
        foreach (var name in names)
        {
            if (!el.TryGetProperty(name, out var p))
                continue;
            if (p.ValueKind == JsonValueKind.String)
                return p.GetString();
        }

        return null;
    }

    private static double ReadJsonDouble(JsonElement el, string name, double defaultValue = 0)
    {
        if (!el.TryGetProperty(name, out var p) || p.ValueKind != JsonValueKind.Number)
            return defaultValue;
        return p.GetDouble();
    }

    private static double? ReadJsonNullableDouble(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.Number => p.GetDouble(),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    private static bool? ReadJsonBool(JsonElement el, string name)
    {
        if (!el.TryGetProperty(name, out var p))
            return null;
        return p.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null
        };
    }

    private static double? ReadNullableDouble(JsonElement data, string name)
    {
        if (!data.TryGetProperty(name, out var el))
            return null;
        return el.ValueKind switch
        {
            JsonValueKind.Number => el.GetDouble(),
            JsonValueKind.Null => null,
            _ => null
        };
    }

    public async Task<AdminAiRequestLogsPageDto> GetRequestLogsAsync(
        DateTime? fromUtc,
        DateTime? toUtc,
        string? actionType,
        string? modelName,
        string? status,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) page = 1;
        if (pageSize < 1) pageSize = 20;
        if (pageSize > 200) pageSize = 200;

        var q = _db.ai_usage_logs.AsNoTracking().AsQueryable();
        if (fromUtc.HasValue)
            q = q.Where(x => x.created_at >= fromUtc.Value);
        if (toUtc.HasValue)
            q = q.Where(x => x.created_at <= toUtc.Value);
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var at = actionType.Trim();
            q = q.Where(x => x.action_type == at);
        }

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var m = modelName.Trim();
            q = q.Where(x => x.model_name != null && x.model_name.Contains(m));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            q = q.Where(x => x.status == s);
        }

        var total = await q.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await q
            .OrderByDescending(x => x.created_at)
            .ThenByDescending(x => x.id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new
            {
                x.id,
                x.created_at,
                x.user_id,
                Email = x.user != null ? x.user.email : null,
                x.story_id,
                x.chapter_id,
                x.action_type,
                x.model_name,
                x.generation_id,
                x.cost_usd,
                x.prompt_tokens,
                x.completion_tokens,
                x.total_tokens,
                x.status
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var items = rows.Select(x => new AdminAiRequestLogItemDto
        {
            Id = x.id,
            CreatedAtUtc = x.created_at,
            UserId = x.user_id,
            UserEmail = x.Email,
            StoryId = x.story_id,
            ChapterId = x.chapter_id,
            ActionType = x.action_type,
            ModelName = x.model_name,
            GenerationId = x.generation_id,
            CostUsd = x.cost_usd,
            PromptTokens = x.prompt_tokens,
            CompletionTokens = x.completion_tokens,
            TotalTokens = x.total_tokens,
            Status = x.status
        }).ToList();

        return new AdminAiRequestLogsPageDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Items = items
        };
    }

    public async Task<AdminAiGenerationsDailyResponseDto> GetGenerationsDailyCountsAsync(
        DateTime fromUtc,
        DateTime toUtc,
        string? modelName,
        string? status,
        string? actionType,
        CancellationToken cancellationToken = default)
    {
        if (toUtc < fromUtc)
            (fromUtc, toUtc) = (toUtc, fromUtc);

        if (toUtc - fromUtc > TimeSpan.FromDays(120))
            fromUtc = toUtc.AddDays(-120);

        var q = _db.ai_usage_logs.AsNoTracking().AsQueryable();
        q = q.Where(x => x.created_at >= fromUtc && x.created_at <= toUtc);
        if (!string.IsNullOrWhiteSpace(actionType))
        {
            var at = actionType.Trim();
            q = q.Where(x => x.action_type == at);
        }

        if (!string.IsNullOrWhiteSpace(modelName))
        {
            var m = modelName.Trim();
            q = q.Where(x => x.model_name != null && x.model_name.Contains(m));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var s = status.Trim();
            q = q.Where(x => x.status == s);
        }

        var rows = await q
            .Where(x => x.created_at != null)
            .GroupBy(x => x.created_at!.Value.Date)
            .Select(g => new { Day = g.Key, Count = g.Count() })
            .OrderBy(x => x.Day)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new AdminAiGenerationsDailyResponseDto
        {
            Days = rows.Select(x => new AdminAiGenerationDayCountDto
            {
                Day = x.Day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                Count = x.Count
            }).ToList()
        };
    }

    public async Task<(bool Ok, string? Error, string? Json)> GetOpenRouterGenerationRawJsonAsync(
        string generationId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(generationId))
            return (false, "generationId bắt buộc.", null);

        var apiKey = await ResolveOpenRouterApiKeyAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrEmpty(apiKey))
        {
            return (false,
                "Chưa tìm thấy key OpenRouter: AI:OpenRouterManagementApiKey / OpenRouterStatsApiKey / OpenRouterApiKey, OPENROUTER_API_KEY, ai_configs, hoặc key đúng với URL openrouter.ai.",
                null);
        }

        if (!IsLikelyOpenRouterSecretKey(apiKey))
        {
            return (false,
                $"Không có secret OpenRouter (sk-or-v1-…). Key được chọn {DescribeRejectedKeyShape(apiKey)} Thêm sk-or-v1-… (AI:OpenRouterManagementApiKey / OpenRouterStatsApiKey / OPENROUTER_API_KEY / ai_configs / slot gắn URL OpenRouter).",
                null);
        }

        try
        {
            using var client = CreateOpenRouterHttpClient();
            var url = "https://openrouter.ai/api/v1/generation?id=" + Uri.EscapeDataString(generationId.Trim());
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Version = HttpVersion.Version11;
            if (!TrySetBearerAuthorization(req, apiKey, out var authErr))
                return (false, authErr, null);
            var resp = await client.SendAsync(req, cancellationToken).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
                return (false, $"OpenRouter /generation HTTP {(int)resp.StatusCode}: {body}", null);
            return (true, null, body);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "OpenRouter /generation failed for {Id}", generationId);
            return (false, ex.Message, null);
        }
    }

    private static bool TrySetBearerAuthorization(HttpRequestMessage req, string apiKey, out string? error)
    {
        error = null;
        req.Headers.Remove("Authorization");
        try
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            return true;
        }
        catch (FormatException)
        {
            if (!req.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey))
            {
                error = "Không gắn được header Authorization (key chứa ký tự không hợp lệ cho HTTP header?).";
                return false;
            }

            return true;
        }
    }
}
