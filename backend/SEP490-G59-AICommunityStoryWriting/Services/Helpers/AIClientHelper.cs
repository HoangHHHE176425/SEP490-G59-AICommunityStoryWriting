using System.ClientModel;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;

namespace Services.Helpers;

/// <summary>Helper dùng chung cho 2 luồng AI: gợi ý chương tiếp theo và đồng sáng tác. Hỗ trợ model theo agent (Planner, Writer, ConsistencyChecker, PlotManager).</summary>
public static class AIClientHelper
{
    public const string AgentPlanner = "Planner";
    public const string AgentWriter = "Writer";
    public const string AgentConsistencyChecker = "ConsistencyChecker";
    public const string AgentPlotManager = "PlotManager";

    public static string GetDefaultModel(string provider)
    {
        if (provider.Equals("Ollama", StringComparison.OrdinalIgnoreCase)) return "llama3";
        if (provider.Equals("Groq", StringComparison.OrdinalIgnoreCase)) return "llama-3.1-8b-instant";
        return "gpt-4o-mini";
    }

    /// <summary>Lấy tên model cho từng agent. Fallback: AI:Model nếu không cấu hình riêng.</summary>
    public static string GetModelForAgent(IConfiguration configuration, string agentName)
    {
        var key = agentName switch
        {
            AgentPlanner => "AI:PlannerModel",
            AgentWriter => "AI:WriterModel",
            AgentConsistencyChecker => "AI:ConsistencyCheckerModel",
            AgentPlotManager => "AI:PlotManagerModel",
            _ => null
        };
        var model = key != null ? configuration[key] : null;
        if (!string.IsNullOrWhiteSpace(model)) return model.Trim();
        var provider = configuration["AI:Provider"] ?? "Ollama";
        return configuration["AI:Model"] ?? GetDefaultModel(provider);
    }

    /// <summary>Đọc cấu hình AI và trả về (provider, model, apiKey, baseUrl). Ném exception nếu thiếu ApiKey khi cần.</summary>
    public static (string provider, string model, string apiKey, string? baseUrl) GetConfig(IConfiguration configuration)
    {
        var provider = configuration["AI:Provider"] ?? "Ollama";
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

    /// <summary>Cấu hình cho một agent: dùng model riêng (Planner/Writer/ConsistencyChecker/PlotManager) nếu có.</summary>
    public static (string provider, string model, string apiKey, string? baseUrl) GetConfigForAgent(IConfiguration configuration, string agentName)
    {
        var (provider, _, apiKey, baseUrl) = GetConfig(configuration);
        var model = GetModelForAgent(configuration, agentName);
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

    /// <summary>Options cho chat completion. Khi dùng OpenAI/Azure/Groq nên set MaxOutputTokenCount (vd. Writer 8192) để không bị cắt output.</summary>
    /// <param name="agentName">AgentPlanner, AgentWriter, AgentConsistencyChecker, AgentPlotManager hoặc null → dùng AI:MaxOutputTokens.</param>
    public static ChatCompletionOptions? GetCompletionOptions(IConfiguration configuration, string? agentName)
    {
        int maxTokens;
        if (agentName == AgentWriter)
        {
            maxTokens = configuration.GetValue("AI:WriterMaxOutputTokens", 8192);
            if (maxTokens < 256) maxTokens = 8192;
        }
        else
        {
            maxTokens = configuration.GetValue("AI:MaxOutputTokens", 4096);
            if (maxTokens < 256) maxTokens = 4096;
        }
        return new ChatCompletionOptions { MaxOutputTokenCount = maxTokens };
    }
}
