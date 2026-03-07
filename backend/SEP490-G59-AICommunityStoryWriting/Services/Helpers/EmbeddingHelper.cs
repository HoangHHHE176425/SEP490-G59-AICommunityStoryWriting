using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Services.Helpers;

/// <summary>Gọi API embedding: Ollama (POST /api/embed) hoặc OpenAI-compatible (POST /embeddings).</summary>
public static class EmbeddingHelper
{
    private const string DefaultEmbeddingModel = "nomic-embed-text";
    private const string DefaultOpenAiEmbeddingModel = "text-embedding-3-small";
    private const int MaxRetriesOn429 = 3;
    private const int DefaultRetryAfterSeconds = 60;

    /// <summary>Lấy cấu hình embedding. Ollama: EmbeddingBaseUrl (vd. http://localhost:11434), EmbeddingModel (nomic-embed-text), không cần ApiKey. OpenAI: thêm EmbeddingApiKey.</summary>
    public static (string baseUrl, string? apiKey, string model)? GetEmbeddingConfig(IConfiguration configuration)
    {
        var baseUrl = configuration["AI:EmbeddingBaseUrl"]?.TrimEnd('/');
        var provider = configuration["AI:EmbeddingProvider"] ?? (IsOllamaUrl(baseUrl) ? "Ollama" : "OpenAI");
        var model = configuration["AI:EmbeddingModel"] ?? (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) ? DefaultEmbeddingModel : DefaultOpenAiEmbeddingModel);

        if (string.IsNullOrWhiteSpace(baseUrl))
            return null;
        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
            return (baseUrl, null, model);
        var apiKey = configuration["AI:EmbeddingApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return null;
        if (!baseUrl.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
            baseUrl += "/v1";
        return (baseUrl, apiKey, model);
    }

    private static bool IsOllamaUrl(string? url) => url != null && (url.Contains("11434") || url.Contains("ollama", StringComparison.OrdinalIgnoreCase));

    /// <summary>Gọi API embedding (một đoạn text), trả về vector.</summary>
    public static async Task<float[]> GetEmbeddingAsync(string text, string baseUrl, string? apiKey, string model, CancellationToken cancellationToken = default)
    {
        var list = await GetEmbeddingsBatchAsync(new[] { text }, baseUrl, apiKey, model, cancellationToken);
        return list.Count > 0 ? list[0] : Array.Empty<float>();
    }

    /// <summary>Batch embedding. Ollama: POST /api/embed với input[]; OpenAI: POST /v1/embeddings.</summary>
    public static async Task<List<float[]>> GetEmbeddingsBatchAsync(string[] texts, string baseUrl, string? apiKey, string model, CancellationToken cancellationToken = default)
    {
        if (texts == null || texts.Length == 0)
            return new List<float[]>();

        if (IsOllamaUrl(baseUrl))
            return await GetEmbeddingsBatchOllamaAsync(texts, baseUrl, model, cancellationToken);

        return await GetEmbeddingsBatchOpenAiAsync(texts, baseUrl, apiKey ?? "", model, cancellationToken);
    }

    private static async Task<List<float[]>> GetEmbeddingsBatchOllamaAsync(string[] texts, string baseUrl, string model, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        http.Timeout = TimeSpan.FromSeconds(120);
        var url = $"{baseUrl}/api/embed";
        var payload = new { model, input = texts.Length == 1 ? (object)texts[0] : texts };
        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await http.PostAsync(url, content, cancellationToken);
        response.EnsureSuccessStatusCode();
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        if (doc.RootElement.TryGetProperty("embeddings", out var embArr))
        {
            var results = new List<float[]>();
            foreach (var item in embArr.EnumerateArray())
            {
                var vec = new List<float>();
                foreach (var e in item.EnumerateArray())
                    vec.Add((float)e.GetDouble());
                results.Add(vec.ToArray());
            }
            return results;
        }
        if (doc.RootElement.TryGetProperty("embedding", out var single))
        {
            var vec = new List<float>();
            foreach (var e in single.EnumerateArray())
                vec.Add((float)e.GetDouble());
            return new List<float[]> { vec.ToArray() };
        }
        return new List<float[]>();
    }

    private static async Task<List<float[]>> GetEmbeddingsBatchOpenAiAsync(string[] texts, string baseUrl, string apiKey, string model, CancellationToken cancellationToken)
    {
        using var http = new HttpClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        http.Timeout = TimeSpan.FromSeconds(120);
        var payload = new { input = texts, model };
        var json = JsonSerializer.Serialize(payload);
        var jsonBytes = Encoding.UTF8.GetBytes(json);
        var response = await PostWithRetryOn429Async(http, $"{baseUrl}/embeddings", jsonBytes, cancellationToken);
        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseJson);
        var data = doc.RootElement.GetProperty("data");
        var results = new List<float[]>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            var embedding = item.GetProperty("embedding");
            var vec = new List<float>();
            foreach (var e in embedding.EnumerateArray())
                vec.Add((float)e.GetDouble());
            results.Add(vec.ToArray());
        }
        return results;
    }

    /// <summary>POST request; nếu 429 thì đợi (Retry-After hoặc mặc định) rồi thử lại, tối đa MaxRetriesOn429 lần. Mỗi lần thử dùng body mới (byte[]) vì HttpContent chỉ gửi được một lần.</summary>
    private static async Task<HttpResponseMessage> PostWithRetryOn429Async(HttpClient http, string url, byte[] jsonBody, CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= MaxRetriesOn429; attempt++)
        {
            using var content = new ByteArrayContent(jsonBody);
            content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
            var response = await http.PostAsync(url, content, cancellationToken);
            if ((int)response.StatusCode == 429)
            {
                if (attempt < MaxRetriesOn429)
                {
                    var waitSeconds = DefaultRetryAfterSeconds;
                    if (response.Headers.RetryAfter?.Delta is { } delta)
                        waitSeconds = (int)Math.Min(Math.Max(delta.TotalSeconds, 1), 120);
                    else if (response.Headers.RetryAfter?.Date is { } retryDate)
                        waitSeconds = (int)Math.Min(Math.Max((retryDate - DateTimeOffset.UtcNow).TotalSeconds, 1), 120);
                    await Task.Delay(TimeSpan.FromSeconds(waitSeconds), cancellationToken);
                    continue;
                }
                throw new InvalidOperationException("API embedding trả 429 (quá nhiều request) sau vài lần thử. Vui lòng đợi 2–3 phút rồi gọi lại POST /api/ai/index-rag.");
            }
            response.EnsureSuccessStatusCode();
            return response;
        }
        throw new InvalidOperationException("API embedding lỗi. Vui lòng thử lại sau.");
    }
}
