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
            var clientId = Require("PayOS:ClientId");
            var apiKey = Require("PayOS:ApiKey");
            var checksumKey = Require("PayOS:ChecksumKey");

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
            var clientId = Require("PayOS:ClientId");
            var apiKey = Require("PayOS:ApiKey");

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

        private static string ComputeHmacSha256(string data, string key)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(data));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}

