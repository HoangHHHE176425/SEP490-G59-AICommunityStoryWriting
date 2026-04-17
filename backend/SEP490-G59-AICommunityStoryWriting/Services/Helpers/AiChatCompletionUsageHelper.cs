using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using OpenAI.Chat;

namespace Services.Helpers;

/// <summary>Ghi nhận id/token/chi phí từ ChatCompletion (OpenRouter/OpenAI-compatible).</summary>
public static class AiChatCompletionUsageHelper
{
    public static (int PromptTokens, int CompletionTokens, int TotalTokens) GetTokenCounts(ChatCompletion completion)
    {
        var p = completion.Usage?.InputTokenCount ?? 0;
        var o = completion.Usage?.OutputTokenCount ?? 0;
        return (p, o, p + o);
    }

    public static string? GetGenerationId(ChatCompletion completion) =>
        string.IsNullOrWhiteSpace(completion.Id) ? null : completion.Id.Trim();

    /// <summary>
    /// OpenRouter thêm <c>usage.cost</c> (USD) vào JSON; OpenAI .NET SDK 2.x đưa các trường lạ vào
    /// <c>SerializedAdditionalRawData</c> của <see cref="ChatTokenUsage"/>. Đọc phản chiếu an toàn, không có thì null.
    /// </summary>
    public static decimal? TryGetOpenRouterCostUsd(ChatCompletion? completion) =>
        completion?.Usage is null ? null : TryReadOpenRouterCostFromUsage(completion.Usage);

    private static decimal? TryReadOpenRouterCostFromUsage(ChatTokenUsage usage)
    {
        try
        {
            foreach (var propName in new[] { "SerializedAdditionalRawData", "_serializedAdditionalRawData" })
            {
                var prop = usage.GetType().GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (prop?.GetValue(usage) is not IDictionary dict)
                    continue;

                foreach (DictionaryEntry entry in dict)
                {
                    if (entry.Key is not string key || !key.Equals("cost", StringComparison.OrdinalIgnoreCase))
                        continue;
                    return ParseCostUsd(entry.Value);
                }
            }
        }
        catch
        {
            // SDK đổi schema — bỏ qua
        }

        return null;
    }

    private static decimal? ParseCostUsd(object? value)
    {
        if (value is null) return null;
        switch (value)
        {
            case decimal d:
                return d;
            case double db:
                return (decimal)db;
            case float f:
                return (decimal)f;
        }

        if (value is BinaryData bd)
        {
            var s = bd.ToString();
            if (string.IsNullOrWhiteSpace(s)) return null;
            s = s.Trim();
            if (decimal.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var dec))
                return dec;
            try
            {
                using var doc = JsonDocument.Parse(s);
                var root = doc.RootElement;
                if (root.ValueKind == JsonValueKind.Number && root.TryGetDecimal(out var j))
                    return j;
                if (root.ValueKind == JsonValueKind.String && root.GetString() is { } inner &&
                    decimal.TryParse(inner.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var innerDec))
                    return innerDec;
            }
            catch
            {
                // ignore
            }
        }

        return null;
    }
}
