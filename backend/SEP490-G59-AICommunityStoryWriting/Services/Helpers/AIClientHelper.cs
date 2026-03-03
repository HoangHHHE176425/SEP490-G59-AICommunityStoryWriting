using System.ClientModel;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace Services.Helpers;

/// <summary>Helper dùng chung cho 2 luồng AI: gợi ý chương tiếp theo và đồng sáng tác.</summary>
public static class AIClientHelper
{
    public static string GetDefaultModel(string provider)
    {
        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)) return "llama3";
        if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)) return "llama-3.1-8b-instant";
        return "gpt-4o-mini";
    }

    /// <summary>Đọc cấu hình AI và trả về (provider, model, apiKey, baseUrl). Ném exception nếu thiếu ApiKey khi cần.</summary>
    public static (string provider, string model, string apiKey, string? baseUrl) GetConfig(IConfiguration configuration)
    {
        var provider = configuration["AI:Provider"] ?? "Groq";
        var model = configuration["AI:Model"] ?? GetDefaultModel(provider);
        var apiKey = configuration["AI:ApiKey"];
        var baseUrl = configuration["AI:BaseUrl"];

        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = "http://localhost:11434/v1";
            apiKey ??= "ollama";
        }
        else if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                baseUrl = "https://api.groq.com/openai/v1";
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Cấu hình AI:ApiKey chưa được thiết lập. Lấy API key miễn phí tại https://console.groq.com/keys và thêm vào appsettings.Local.json.");
        }
        else if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("Cấu hình AI:ApiKey chưa được thiết lập. Vui lòng thêm vào appsettings.Local.json (hoặc dùng AI:Provider = Groq/Ollama).");

        return (provider, model, apiKey!, baseUrl);
    }

    public static ChatClient CreateChatClient(string provider, string model, string apiKey, string? baseUrl)
    {
        if ((provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase) || provider.Equals("Groq", StringComparison.OrdinalIgnoreCase))
            && !string.IsNullOrWhiteSpace(baseUrl))
        {
            var endpoint = baseUrl.TrimEnd('/');
            if (!endpoint.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
                endpoint += "/v1";
            var options = new OpenAIClientOptions { Endpoint = new Uri(endpoint) };
            var credential = new ApiKeyCredential(apiKey);
            var openAiClient = new OpenAIClient(credential, options);
            return openAiClient.GetChatClient(model);
        }
        return new ChatClient(model, apiKey);
    }
}
