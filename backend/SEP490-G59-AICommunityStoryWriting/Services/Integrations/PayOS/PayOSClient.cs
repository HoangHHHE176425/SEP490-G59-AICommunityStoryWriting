using Microsoft.Extensions.Configuration;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Services.Integrations.PayOS
{
    public class PayOSClient
    {
        private readonly HttpClient _http;
        private readonly IConfiguration _config;

        public PayOSClient(HttpClient http, IConfiguration config)
        {
            _http = http;
            _config = config;
        }

        public sealed record CreatePaymentLinkResult(
            string PaymentLinkId,
            string CheckoutUrl,
            string RawResponse,
            string? Code = null
        );

        public sealed record GetPaymentRequestResult(
            string Id,
            string Status,
            long OrderCode,
            long Amount,
            long AmountPaid,
            long AmountRemaining,
            DateTimeOffset? CreatedAt,
            DateTimeOffset? CanceledAt,
            string RawResponse,
            string? Code = null
        );

        public sealed record PayoutBatchItem(
            string ReferenceId,
            long Amount,
            string Description,
            string ToBin,
            string ToAccountNumber
        );

        public sealed record PayoutBatchRequest(
            string ReferenceId,
            IReadOnlyList<string> Category,
            bool ValidateDestination,
            IReadOnlyList<PayoutBatchItem> Payouts
        );

        public sealed record CreatePayoutBatchResult(
            string PayoutId,
            string ReferenceId,
            string ApprovalState,
            string RawResponse
        );

        public sealed record GetPayoutResult(
            string PayoutId,
            string ReferenceId,
            string ApprovalState,
            string? FirstTransactionState,
            string RawResponse
        );

        public sealed record PayoutAccountBalanceResult(
            string Code,
            string Desc,
            decimal? Balance,
            string RawResponse
        );

        public async Task<CreatePaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode,
            decimal amountVnd,
            string description,
            string cancelUrl,
            string returnUrl,
            int? expiredAt = null,
            CancellationToken cancellationToken = default)
        {
            var baseUrl = Require("PayOS:BaseUrl").TrimEnd('/');
            var rootUrl = baseUrl;
            // Allow BaseUrl to be either "...", ".../v1" or ".../v2".
            if (rootUrl.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            else if (rootUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            var clientId = Require("PayOS:ClientId").Trim();
            var apiKey = Require("PayOS:ApiKey").Trim();
            var checksumKey = Require("PayOS:ChecksumKey").Trim();

            if (amountVnd <= 0) throw new ArgumentException("Amount must be > 0", nameof(amountVnd));
            if (amountVnd != decimal.Truncate(amountVnd)) throw new ArgumentException("Amount must be an integer value in VND", nameof(amountVnd));

            var amount = amountVnd.ToString("0");

            var sorted = new SortedDictionary<string, string>
            {
                { "amount", amount },
                { "cancelUrl", cancelUrl },
                { "description", description },
                { "orderCode", orderCode.ToString() },
                { "returnUrl", returnUrl }
            };

            var rawData = string.Join("&", sorted.Select(kv => $"{kv.Key}={kv.Value}"));
            var signature = ComputeHmacSha256(rawData, checksumKey);

            // NOTE: Per PayOS docs, signature is computed from:
            // amount, cancelUrl, description, orderCode, returnUrl (sorted by alphabet).
            // expiredAt is NOT part of the signature payload.
            object body = expiredAt.HasValue
                ? new
                {
                    orderCode,
                    amount = long.Parse(amount),
                    description,
                    cancelUrl,
                    returnUrl,
                    expiredAt = expiredAt.Value,
                    signature
                }
                : new
                {
                    orderCode,
                    amount = long.Parse(amount),
                    description,
                    cancelUrl,
                    returnUrl,
                    signature
                };

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{baseUrl}/payment-requests");
            req.Headers.Add("x-client-id", clientId);
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            req.Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

            using var res = await _http.SendAsync(req, cancellationToken);
            var text = await res.Content.ReadAsStringAsync(cancellationToken);

            if (!res.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"PayOS error {(int)res.StatusCode}: {text}");
            }

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var codeEl) ? codeEl.ToString() : null;
            if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException($"PayOS response missing data: {text}");
            }

            var paymentLinkId = dataEl.TryGetProperty("paymentLinkId", out var pl) ? pl.ToString() : null;
            var checkoutUrl = dataEl.TryGetProperty("checkoutUrl", out var cu) ? cu.ToString() : null;

            if (string.IsNullOrWhiteSpace(paymentLinkId) || string.IsNullOrWhiteSpace(checkoutUrl))
            {
                throw new InvalidOperationException($"PayOS response missing paymentLinkId/checkoutUrl: {text}");
            }

            return new CreatePaymentLinkResult(paymentLinkId!, checkoutUrl!, text, code);
        }

        public async Task<GetPaymentRequestResult> GetPaymentRequestAsync(string id, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(id)) throw new ArgumentException("id is required", nameof(id));

            var baseUrl = Require("PayOS:BaseUrl").TrimEnd('/');
            var rootUrl = baseUrl;
            // Allow BaseUrl to be either "...", ".../v1" or ".../v2".
            if (rootUrl.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            else if (rootUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            var clientId = Require("PayOS:ClientId").Trim();
            var apiKey = Require("PayOS:ApiKey").Trim();

            using var req = new HttpRequestMessage(HttpMethod.Get, $"{baseUrl}/payment-requests/{id}");
            req.Headers.Add("x-client-id", clientId);
            req.Headers.Add("x-api-key", apiKey);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var res = await _http.SendAsync(req, cancellationToken);
            var text = await res.Content.ReadAsStringAsync(cancellationToken);

            if (!res.IsSuccessStatusCode)
                throw new InvalidOperationException($"PayOS error {(int)res.StatusCode}: {text}");

            using var doc = JsonDocument.Parse(text);
            var root = doc.RootElement;

            var code = root.TryGetProperty("code", out var codeEl) ? codeEl.ToString() : null;

            if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
                throw new InvalidOperationException($"PayOS response missing data: {text}");

            // Verify response signature if present
            if (root.TryGetProperty("signature", out var sigEl))
            {
                var signature = sigEl.ToString() ?? string.Empty;
                if (!string.IsNullOrWhiteSpace(signature))
                {
                    var expected = ComputeWebhookSignature(dataEl);
                    if (!string.Equals(signature, expected, StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException("Invalid PayOS response signature");
                }
            }

            var payosId = dataEl.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
            var status = dataEl.TryGetProperty("status", out var stEl) ? stEl.ToString() : null;
            var orderCode = dataEl.TryGetProperty("orderCode", out var ocEl) ? ocEl.GetInt64() : 0;
            var amount = dataEl.TryGetProperty("amount", out var amtEl) ? amtEl.GetInt64() : 0;
            var amountPaid = dataEl.TryGetProperty("amountPaid", out var apEl) ? apEl.GetInt64() : 0;
            var amountRemaining = dataEl.TryGetProperty("amountRemaining", out var arEl) ? arEl.GetInt64() : 0;

            DateTimeOffset? createdAt = null;
            if (dataEl.TryGetProperty("createdAt", out var caEl) && DateTimeOffset.TryParse(caEl.ToString(), out var ca))
                createdAt = ca;

            DateTimeOffset? canceledAt = null;
            if (dataEl.TryGetProperty("canceledAt", out var cancEl) && DateTimeOffset.TryParse(cancEl.ToString(), out var canc))
                canceledAt = canc;

            if (string.IsNullOrWhiteSpace(payosId) || string.IsNullOrWhiteSpace(status))
                throw new InvalidOperationException($"PayOS response missing id/status: {text}");

            return new GetPaymentRequestResult(
                payosId!,
                status!,
                orderCode,
                amount,
                amountPaid,
                amountRemaining,
                createdAt,
                canceledAt,
                text,
                code
            );
        }

        public async Task<CreatePayoutBatchResult> CreatePayoutBatchAsync(
            PayoutBatchRequest request,
            string? idempotencyKey = null,
            CancellationToken cancellationToken = default)
        {
            var baseUrl = Require("PayOS:BaseUrl").TrimEnd('/');
            var rootUrl = baseUrl;
            // Allow BaseUrl to be either "...", ".../v1" or ".../v2".
            if (rootUrl.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            else if (rootUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            var clientId = Require("PayOS:PayoutClientId").Trim();
            var apiKey = Require("PayOS:PayoutApiKey").Trim();
            var checksumKey = Require("PayOS:PayoutChecksumKey").Trim();

            string Mask(string? v)
            {
                var s = v ?? string.Empty;
                if (s.Length <= 8) return new string('*', s.Length);
                return $"{s[..4]}...{s[^4]}";
            }

            if (string.IsNullOrWhiteSpace(request.ReferenceId)) throw new ArgumentException("ReferenceId is required.", nameof(request));
            if (request.Payouts == null || request.Payouts.Count == 0) throw new ArgumentException("At least one payout item is required.", nameof(request));

            var body = new Dictionary<string, object?>
            {
                ["referenceId"] = request.ReferenceId,
                ["category"] = request.Category,
                ["validateDestination"] = request.ValidateDestination,
                ["payouts"] = request.Payouts.Select(p => new Dictionary<string, object?>
                {
                    ["referenceId"] = p.ReferenceId,
                    ["amount"] = p.Amount,
                    ["description"] = p.Description,
                    ["toBin"] = p.ToBin,
                    ["toAccountNumber"] = p.ToAccountNumber
                }).ToList<object?>()
            };

            var resolvedIdempotencyKey = idempotencyKey ?? Guid.NewGuid().ToString();

            var candidateRequests = new List<(string Url, Dictionary<string, object?> Body, string Mode)>
            {
                ($"{rootUrl}/v1/payouts/batch", body, "batch"),
            };

            // Official SDK also supports single payout endpoint (/v1/payouts/).
            // Some merchant accounts may not expose /batch, so we fallback when there's only 1 payout item.
            if (request.Payouts.Count == 1)
            {
                var p = request.Payouts[0];
                var singleBody = new Dictionary<string, object?>
                {
                    ["referenceId"] = p.ReferenceId,
                    ["amount"] = p.Amount,
                    ["description"] = p.Description,
                    ["toBin"] = p.ToBin,
                    ["toAccountNumber"] = p.ToAccountNumber,
                    ["category"] = request.Category
                };
                candidateRequests.Add(($"{rootUrl}/v1/payouts/", singleBody, "single"));
            }

            string? lastError = null;
            for (var i = 0; i < candidateRequests.Count; i++)
            {
                var candidate = candidateRequests[i];
                var url = candidate.Url;

                // PayOS may rate-limit batch payouts; wait+retry a couple of times.
                const int max429Retries = 2;
                var retry429Count = 0;

                while (true)
                {
                    // PayOS uses x-signature computed from the request body payload.
                    var signature = CreateRequestSignature(checksumKey, candidate.Body);
                    var signaturePreview = signature.Length <= 12 ? signature : $"{signature[..6]}...{signature[^6..]}";

                    using var httpReq = new HttpRequestMessage(HttpMethod.Post, url);
                    httpReq.Headers.Add("x-client-id", clientId);
                    httpReq.Headers.Add("x-api-key", apiKey);
                    httpReq.Headers.Add("x-idempotency-key", resolvedIdempotencyKey);
                    httpReq.Headers.Add("x-signature", signature);
                    httpReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                    httpReq.Content = new StringContent(JsonSerializer.Serialize(candidate.Body), Encoding.UTF8, "application/json");

                    using var httpRes = await _http.SendAsync(httpReq, cancellationToken);
                    var text = await httpRes.Content.ReadAsStringAsync(cancellationToken);

                    if (httpRes.IsSuccessStatusCode)
                    {
                        using var doc = JsonDocument.Parse(text);
                        var root = doc.RootElement;
                        // PayOS lỗi thường trả { code, desc, data:null } với HTTP 200.
                        if (root.TryGetProperty("code", out var codeEl) || root.TryGetProperty("desc", out var descEl))
                        {
                            var code = root.TryGetProperty("code", out var cEl) ? cEl.ToString() : null;
                            var desc = root.TryGetProperty("desc", out var dEl) ? dEl.ToString() : null;
                            var dataKind = root.TryGetProperty("data", out var dEl2) ? dEl2.ValueKind : JsonValueKind.Undefined;
                            if (dataKind != JsonValueKind.Object)
                                throw new InvalidOperationException(
                                    $"PayOS payout failed. code={code}, desc={desc}, mode={candidate.Mode}, url={url}, clientId={Mask(clientId)}, apiKey={Mask(apiKey)}, x-signature={signaturePreview}");
                        }

                        if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
                            throw new InvalidOperationException(
                                $"PayOS response missing data: mode={candidate.Mode}, url={url}, clientId={Mask(clientId)}, apiKey={Mask(apiKey)}, x-signature={signaturePreview}, body={text}");

                        var payoutId = dataEl.TryGetProperty("id", out var idEl) ? idEl.ToString() : null;
                        var referenceId = dataEl.TryGetProperty("referenceId", out var refEl) ? refEl.ToString() : request.ReferenceId;
                        var approvalState = dataEl.TryGetProperty("approvalState", out var asEl) ? asEl.ToString() : "UNKNOWN";

                        if (string.IsNullOrWhiteSpace(payoutId))
                            throw new InvalidOperationException($"PayOS response missing payout id: {text}");

                        return new CreatePayoutBatchResult(payoutId!, referenceId ?? request.ReferenceId, approvalState ?? "UNKNOWN", text);
                    }

                    lastError = $"PayOS error {(int)httpRes.StatusCode} for {url} (mode={candidate.Mode}): {text}";

                    // Handle rate-limiting: 429
                    if (httpRes.StatusCode == (System.Net.HttpStatusCode)429 && retry429Count < max429Retries)
                    {
                        var retryAfterDelay = TimeSpan.FromSeconds(2 * (retry429Count + 1));
                        if (httpRes.Headers.TryGetValues("Retry-After", out var retryAfterValues))
                        {
                            var retryAfter = retryAfterValues.FirstOrDefault();
                            if (int.TryParse(retryAfter, out var retrySeconds) && retrySeconds > 0)
                                retryAfterDelay = TimeSpan.FromSeconds(retrySeconds);
                        }

                        retry429Count++;
                        await Task.Delay(retryAfterDelay, cancellationToken);
                        continue;
                    }

                    // Retry on 404 only (endpoint version mismatch). For other errors, stop.
                    if (httpRes.StatusCode != System.Net.HttpStatusCode.NotFound)
                        break;

                    // 404 => try next candidate url
                    break;
                }
            }

            throw new InvalidOperationException(lastError ?? "PayOS payout batch failed.");
        }

        public async Task<GetPayoutResult> GetPayoutInfoAsync(string payoutId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(payoutId)) throw new ArgumentException("payoutId is required.", nameof(payoutId));

            var baseUrl = Require("PayOS:BaseUrl").TrimEnd('/');
            var rootUrl = baseUrl;
            // Allow BaseUrl to be either "...", ".../v1" or ".../v2".
            if (rootUrl.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            else if (rootUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            var clientId = Require("PayOS:PayoutClientId").Trim();
            var apiKey = Require("PayOS:PayoutApiKey").Trim();

            var candidateUrls = new List<string>
            {
                $"{rootUrl}/v1/payouts/{payoutId}",
                $"{rootUrl}/v2/payouts/{payoutId}",
            };

            string? lastError = null;
            for (var i = 0; i < candidateUrls.Count; i++)
            {
                var url = candidateUrls[i];
                using var httpReq = new HttpRequestMessage(HttpMethod.Get, url);
                httpReq.Headers.Add("x-client-id", clientId);
                httpReq.Headers.Add("x-api-key", apiKey);
                httpReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                using var httpRes = await _http.SendAsync(httpReq, cancellationToken);
                var text = await httpRes.Content.ReadAsStringAsync(cancellationToken);

                if (httpRes.IsSuccessStatusCode)
                {
                    using var doc = JsonDocument.Parse(text);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("code", out var codeEl) || root.TryGetProperty("desc", out var descEl))
                    {
                        var code = root.TryGetProperty("code", out var cEl) ? cEl.ToString() : null;
                        var desc = root.TryGetProperty("desc", out var dEl) ? dEl.ToString() : null;
                        var dataKind = root.TryGetProperty("data", out var dEl2) ? dEl2.ValueKind : JsonValueKind.Undefined;
                        if (dataKind != JsonValueKind.Object)
                            throw new InvalidOperationException($"PayOS get payout failed. code={code}, desc={desc}, url={url}");
                    }

                    if (!root.TryGetProperty("data", out var dataEl) || dataEl.ValueKind != JsonValueKind.Object)
                        throw new InvalidOperationException($"PayOS response missing data: url={url}, body={text}");

                    var id = dataEl.TryGetProperty("id", out var idEl) ? idEl.ToString() : payoutId;
                    var referenceId = dataEl.TryGetProperty("referenceId", out var refEl) ? refEl.ToString() : null;
                    var approvalState = dataEl.TryGetProperty("approvalState", out var asEl) ? asEl.ToString() : "UNKNOWN";

                    string? firstTxState = null;
                    if (dataEl.TryGetProperty("transactions", out var txsEl) && txsEl.ValueKind == JsonValueKind.Object)
                    {
                        // Some responses may use object-indexed transactions; best-effort parse.
                        foreach (var txProp in txsEl.EnumerateObject())
                        {
                            if (txProp.Value.ValueKind != JsonValueKind.Object) continue;
                            if (txProp.Value.TryGetProperty("state", out var stEl))
                            {
                                firstTxState = stEl.ToString();
                                break;
                            }
                        }
                    }
                    else if (dataEl.TryGetProperty("transactions", out var txArrEl) && txArrEl.ValueKind == JsonValueKind.Array)
                    {
                        if (txArrEl.GetArrayLength() > 0)
                        {
                            var first = txArrEl[0];
                            if (first.ValueKind == JsonValueKind.Object && first.TryGetProperty("state", out var stEl))
                                firstTxState = stEl.ToString();
                        }
                    }

                    return new GetPayoutResult(id ?? payoutId, referenceId ?? "", approvalState ?? "UNKNOWN", firstTxState, text);
                }

                lastError = $"PayOS error {(int)httpRes.StatusCode} for {url}: {text}";

                // Retry on 404 only (endpoint version mismatch).
                if (httpRes.StatusCode != System.Net.HttpStatusCode.NotFound)
                    break;
            }

            throw new InvalidOperationException(lastError ?? "PayOS get payout failed.");
        }

        /// <summary>
        /// Endpoint không cần x-signature. Dùng để kiểm tra nhanh x-client-id/x-api-key có hợp lệ cho payouts không.
        /// </summary>
        public async Task<PayoutAccountBalanceResult> GetPayoutAccountBalanceAsync(CancellationToken cancellationToken = default)
        {
            var baseUrl = Require("PayOS:BaseUrl").TrimEnd('/');
            var rootUrl = baseUrl;
            // Allow BaseUrl to be either "...", ".../v1" or ".../v2".
            if (rootUrl.EndsWith("/v2", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];
            else if (rootUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                rootUrl = rootUrl[..^3];

            var clientId = Require("PayOS:PayoutClientId").Trim();
            var apiKey = Require("PayOS:PayoutApiKey").Trim();

            // PayOS docs: chỉ có GET /v1/payouts-account/balance.
            // https://payos.vn/docs/api#tag/payout-account/operation/get-account-balance
            var candidateUrls = new List<string>
            {
                $"{rootUrl}/v1/payouts-account/balance",
            };

            // Debug: xác nhận public IP thực tế mà backend đang dùng
            string? publicIp = null;
            try
            {
                using var ipCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                ipCts.CancelAfter(TimeSpan.FromSeconds(3));
                publicIp = (await _http.GetStringAsync("https://api.ipify.org", ipCts.Token)).Trim();
            }
            catch
            {
                // Ignore: nếu không lấy được IP thì vẫn cho PayOS call chạy bình thường.
            }

            string Mask(string? v)
            {
                var s = v ?? string.Empty;
                if (s.Length <= 8) return new string('*', s.Length);
                return $"{s[..4]}...{s[^4]}";
            }

            string? lastError = null;
            var attemptErrors = new List<string>();
            for (var i = 0; i < candidateUrls.Count; i++)
            {
                var url = candidateUrls[i];
                using var httpReq = new HttpRequestMessage(HttpMethod.Get, url);
                httpReq.Headers.Add("x-client-id", clientId);
                httpReq.Headers.Add("x-api-key", apiKey);
                httpReq.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

                using var httpRes = await _http.SendAsync(httpReq, cancellationToken);
                var text = await httpRes.Content.ReadAsStringAsync(cancellationToken);

                if (!httpRes.IsSuccessStatusCode)
                {
                    lastError = $"PayOS error {(int)httpRes.StatusCode} for {url} (clientId={Mask(clientId)}, apiKey={Mask(apiKey)}, publicIp={publicIp ?? "?"}): {text}";
                    attemptErrors.Add($"{(int)httpRes.StatusCode} {url} body={text}");
                    continue;
                }

                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                var code = root.TryGetProperty("code", out var cEl) ? cEl.ToString() ?? "" : "";
                var desc = root.TryGetProperty("desc", out var dEl) ? dEl.ToString() ?? "" : "";

                if (root.TryGetProperty("data", out var dataEl) && dataEl.ValueKind == JsonValueKind.Object)
                {
                    decimal? balance = null;
                    // docs show data.balance as string; best-effort parse.
                    if (dataEl.TryGetProperty("balance", out var balEl))
                    {
                        if (balEl.ValueKind == JsonValueKind.Number && balEl.TryGetDecimal(out var b1))
                            balance = b1;
                        else if (balEl.ValueKind == JsonValueKind.String && decimal.TryParse(balEl.ToString(), out var b2))
                            balance = b2;
                    }
                    return new PayoutAccountBalanceResult(code, desc, balance, text);
                }

                // success HTTP but missing data (often error code with data=null)
                throw new InvalidOperationException(
                    $"PayOS payout account balance response missing data. code={code}, desc={desc}, url={url}, clientId={Mask(clientId)}, apiKey={Mask(apiKey)}, publicIp={publicIp ?? "?"}, body={text}");
            }

            throw new InvalidOperationException(
                lastError ??
                $"PayOS get payout account balance failed. Attempts: {string.Join(" | ", attemptErrors)}");
        }

        public string ComputeWebhookSignature(JsonElement dataObject)
        {
            var checksumKey = Require("PayOS:ChecksumKey");

            if (dataObject.ValueKind != JsonValueKind.Object)
                throw new ArgumentException("Webhook data must be a JSON object", nameof(dataObject));

            var dict = new SortedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var prop in dataObject.EnumerateObject())
            {
                dict[prop.Name] = prop.Value.ToString() ?? string.Empty;
            }

            var raw = string.Join("&", dict.Select(kv => $"{kv.Key}={kv.Value}"));
            return ComputeHmacSha256(raw, checksumKey);
        }

        private string Require(string key)
        {
            var value = _config[key];
            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Missing configuration: '{key}'");
            return value;
        }

        private string RequireOrFallback(string primaryKey, string fallbackKey)
        {
            var primary = _config[primaryKey];
            if (!string.IsNullOrWhiteSpace(primary))
                return primary.Trim();
            return Require(fallbackKey);
        }

        private static string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private static object? DeepSortForSignature(object? value, bool sortArrays)
        {
            if (value is null) return null;

            if (value is Dictionary<string, object?> dict)
            {
                var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kv in dict.OrderBy(k => k.Key, StringComparer.Ordinal))
                {
                    sorted[kv.Key] = DeepSortForSignature(kv.Value, sortArrays);
                }
                return sorted;
            }

            if (value is SortedDictionary<string, object?> sortedDict)
            {
                // Already sorted; still deep sort children.
                var sorted = new SortedDictionary<string, object?>(StringComparer.Ordinal);
                foreach (var kv in sortedDict)
                {
                    sorted[kv.Key] = DeepSortForSignature(kv.Value, sortArrays);
                }
                return sorted;
            }

            if (value is IReadOnlyList<object?> list)
            {
                var outList = new List<object?>(list.Count);
                foreach (var item in list)
                    outList.Add(DeepSortForSignature(item, sortArrays));

                if (sortArrays)
                {
                    // Deterministic ordering for arrays-of-objects when signature doesn't depend on original ordering.
                    var jsonOptions = new JsonSerializerOptions { WriteIndented = false };
                    outList.Sort((a, b) => string.Compare(
                        JsonSerializer.Serialize(a, jsonOptions),
                        JsonSerializer.Serialize(b, jsonOptions),
                        StringComparison.Ordinal));
                }

                return outList;
            }

            if (value is IEnumerable<string> strList)
            {
                return strList.ToList();
            }

            return value;
        }

        private static string CreateRequestSignature(string checksumKey, Dictionary<string, object?> body)
        {
            // Mirror PayOS Node SDK: deepSortObj + encodeURIComponent on key/value + HMACSHA256(hex).
            // Important: PayOS signature expects deep-sorted objects, but array order should be preserved.
            // If we reorder arrays (sortArrays=true), PayOS may reject with code=201 (invalid signature).
            var canonical = DeepSortForSignature(body, sortArrays: false);
            if (canonical is not SortedDictionary<string, object?> canonicalRoot)
            {
                throw new InvalidOperationException("Failed to canonicalize payout request body for signature.");
            }

            // Critical for signature: keep Unicode characters as-is (do not escape to \uXXXX),
            // to match JS JSON.stringify + encodeURIComponent behavior.
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = false,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var parts = new List<string>(canonicalRoot.Count);
            foreach (var kv in canonicalRoot)
            {
                var key = kv.Key;
                var v = kv.Value;

                string valueStr;
                if (v is null)
                {
                    valueStr = string.Empty;
                }
                else if (v is bool b)
                {
                    valueStr = b ? "true" : "false";
                }
                else if (v is string s)
                {
                    valueStr = s;
                }
                else if (v is long l)
                {
                    valueStr = l.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (v is int i)
                {
                    valueStr = i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (v is decimal d)
                {
                    valueStr = d.ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    // JSON stringify for arrays/objects.
                    valueStr = JsonSerializer.Serialize(v, jsonOptions);
                }

                parts.Add($"{EncodeURIComponent(key)}={EncodeURIComponent(valueStr)}");
            }

            var queryString = string.Join("&", parts);
            return ComputeHmacSha256(queryString, checksumKey);
        }

        // Match JavaScript encodeURIComponent behavior:
        // keep: A-Z a-z 0-9 - _ . ! ~ * ' ( )
        // escape everything else using UTF-8 percent encoding.
        private static string EncodeURIComponent(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            var sb = new StringBuilder(s.Length * 3);

            foreach (var rune in s.EnumerateRunes())
            {
                // keep: A-Z a-z 0-9 - _ . ! ~ * ' ( )
                var isAscii =
                    rune.Value <= 0x7F &&
                    ((rune.Value >= 'a' && rune.Value <= 'z') ||
                     (rune.Value >= 'A' && rune.Value <= 'Z') ||
                     (rune.Value >= '0' && rune.Value <= '9') ||
                     rune.Value == '-' || rune.Value == '_' || rune.Value == '.' || rune.Value == '!' ||
                     rune.Value == '~' || rune.Value == '*' || rune.Value == '\'' || rune.Value == '(' || rune.Value == ')');

                if (isAscii)
                {
                    sb.Append(rune.ToString());
                    continue;
                }

                var bytes = Encoding.UTF8.GetBytes(rune.ToString());
                foreach (var b in bytes)
                    sb.Append('%').Append(b.ToString("X2"));
            }

            return sb.ToString();
        }
    }
}

