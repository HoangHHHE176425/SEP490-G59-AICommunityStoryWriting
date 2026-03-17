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

        (provider, apiKey, baseUrl) = NormalizeProviderConfig(provider, apiKey, baseUrl);

        return (provider, model, apiKey!, baseUrl);
    }

    private static (string provider, string? apiKey, string? baseUrl) NormalizeProviderConfig(string provider, string? apiKey, string? baseUrl)
    {
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
                throw new InvalidOperationException("Cấu hình AI:ApiKey chưa được thiết lập (AI:Provider=Groq). Vui lòng thêm key vào cấu hình local (appsettings.{ENV}.Local.json / appsettings.Local.json) hoặc env vars.");
        }
        else
        {
            // OpenAI/OpenRouter/Azure(OpenAI-compatible)/...: yêu cầu apiKey.
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException("Cấu hình AI ApiKey chưa được thiết lập. Vui lòng thêm key vào cấu hình local (appsettings.{ENV}.Local.json / appsettings.Local.json) hoặc env vars.");
        }

        return (provider, apiKey, baseUrl);
    }

    /// <summary>Cấu hình cho một agent: dùng model riêng (Planner/Writer/ConsistencyChecker/PlotManager) nếu có.</summary>
    public static (string provider, string model, string apiKey, string? baseUrl) GetConfigForAgent(IConfiguration configuration, string agentName)
    {
        // Hỗ trợ 2 provider khác nhau:
        // - Analysis (Planner/ConsistencyChecker/PlotManager): AI:AnalysisProvider/BaseUrl/ApiKey
        // - Writing (Writer): AI:WritingProvider/BaseUrl/ApiKey
        // Fallback: AI:Provider/BaseUrl/ApiKey

        string provider;
        string? apiKey;
        string? baseUrl;

        if (agentName == AgentWriter)
        {
            provider = configuration["AI:WritingProvider"] ?? configuration["AI:Provider"] ?? "Ollama";
            apiKey = configuration["AI:WritingApiKey"] ?? configuration["AI:ApiKey"];
            baseUrl = configuration["AI:WritingBaseUrl"] ?? configuration["AI:BaseUrl"];
        }
        else
        {
            provider = configuration["AI:AnalysisProvider"] ?? configuration["AI:Provider"] ?? "Ollama";
            apiKey = configuration["AI:AnalysisApiKey"] ?? configuration["AI:ApiKey"];
            baseUrl = configuration["AI:AnalysisBaseUrl"] ?? configuration["AI:BaseUrl"];
        }

        (provider, apiKey, baseUrl) = NormalizeProviderConfig(provider, apiKey, baseUrl);

        var model = GetModelForAgent(configuration, agentName);
        return (provider, model, apiKey!, baseUrl);
    }

    public static ChatClient CreateChatClient(string provider, string model, string apiKey, string? baseUrl)
    {
        // If a BaseUrl is provided, treat it as an OpenAI-compatible endpoint (e.g. Groq, Ollama, OpenRouter, Azure OpenAI compatible gateways).
        // This allows using a single SDK and switching providers purely via configuration.
        if (!string.IsNullOrWhiteSpace(baseUrl))
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
