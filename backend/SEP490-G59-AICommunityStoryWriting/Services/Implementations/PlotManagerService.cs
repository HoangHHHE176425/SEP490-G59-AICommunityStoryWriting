using System.Text.Json;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories;
using Repositories.Interfaces;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Agent 4 – Plot Manager (Llama 3): cập nhật timeline, event memory, character state, story state từ nội dung chương mới.</summary>
public class PlotManagerService : IPlotManagerService
{
    private const string ActionPlotManager = "PLOT_MANAGER";

    private readonly IStoryContextBuilder _contextBuilder;
    private readonly IStoryCharacterMemoryRepository _characterRepo;
    private readonly IStoryEventMemoryRepository _eventRepo;
    private readonly IStoryStoryStateRepository _stateRepo;
    private readonly IStoryRagService _ragService;
    private readonly IStoryRepository _storyRepository;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IConfiguration _configuration;

    public PlotManagerService(
        IStoryContextBuilder contextBuilder,
        IStoryCharacterMemoryRepository characterRepo,
        IStoryEventMemoryRepository eventRepo,
        IStoryStoryStateRepository stateRepo,
        IStoryRagService ragService,
        IStoryRepository storyRepository,
        IAIUsageLogRepository aiUsageLogRepository,
        IConfiguration configuration)
    {
        _contextBuilder = contextBuilder;
        _characterRepo = characterRepo;
        _eventRepo = eventRepo;
        _stateRepo = stateRepo;
        _ragService = ragService;
        _storyRepository = storyRepository;
        _aiUsageLogRepository = aiUsageLogRepository;
        _configuration = configuration;
    }

    public async Task UpdateMemoryFromChapterAsync(Guid storyId, Guid chapterId, string chapterContent, bool reIndexRagAfter, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(chapterContent))
            return;

        var story = _storyRepository.GetById(storyId);
        if (story == null) return;

        var contextBlock = _contextBuilder.GetStoryAndMemoryBlock(storyId, afterChapterId: chapterId);
        var combined = contextBlock + "\n" + chapterContent;
        var sampleForLanguage = combined.Length > 2500 ? combined[..2500] : combined;
        var storyLanguage = StoryLanguageHelper.DetectFromStoryContext(sampleForLanguage);
        var languageInstruction = StoryLanguageHelper.GetLanguageInstruction(storyLanguage);

        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlotManager);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

        var systemPrompt = """
Bạn là Plot Manager: từ ngữ cảnh truyện và nội dung chương mới, trích xuất (1) sự kiện mới cho timeline, (2) cập nhật trạng thái nhân vật, (3) cập nhật story state (thế giới, quy tắc). Trả về DUY NHẤT một JSON hợp lệ, không markdown:
{
  "timelineUpdates": [ { "description": "Mô tả ngắn sự kiện" } ],
  "characterStateUpdates": [ { "characterName": "Tên nhân vật", "stateJson": "Mô tả trạng thái/tính cách" } ],
  "storyStateUpdate": "Mô tả ngắn trạng thái truyện / quy tắc thế giới (hoặc null nếu không đổi)"
}
Nếu không có gì mới: timelineUpdates/characterStateUpdates dùng [], storyStateUpdate null.
""";

        var userPrompt = $"Ngữ cảnh truyện (các chương trước + tóm tắt):\n\n{contextBlock}\n\n---\n\nNội dung chương mới cần trích xuất:\n\n{chapterContent}\n\n{languageInstruction}\n\nTrả về JSON theo đúng cấu trúc trên.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var completion = await client.CompleteChatAsync(messages);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
            return;

        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var start = text.IndexOf('\n') + 1;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start) text = text[start..end];
        }

        try
        {
            var root = JsonDocument.Parse(text).RootElement;
            var nextOrder = _eventRepo.GetNextOrderIndex(storyId);

            if (root.TryGetProperty("timelineUpdates", out var tu) && tu.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in tu.EnumerateArray())
                {
                    var desc = item.TryGetProperty("description", out var d) ? d.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(desc))
                    {
                        _eventRepo.Add(storyId, chapterId, nextOrder++, desc.Trim());
                    }
                }
            }

            if (root.TryGetProperty("characterStateUpdates", out var csu) && csu.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in csu.EnumerateArray())
                {
                    var name = item.TryGetProperty("characterName", out var n) ? n.GetString() : null;
                    var state = item.TryGetProperty("stateJson", out var s) ? s.GetString() : null;
                    if (!string.IsNullOrWhiteSpace(name))
                        _characterRepo.Upsert(storyId, name.Trim(), state?.Trim());
                }
            }

            if (root.TryGetProperty("storyStateUpdate", out var ssu))
            {
                var val = ssu.ValueKind == JsonValueKind.String ? ssu.GetString() : ssu.GetRawText();
                if (!string.IsNullOrWhiteSpace(val))
                    _stateRepo.Upsert(storyId, val.Trim());
            }

            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = story.author_id ?? Guid.Empty,
                story_id = storyId,
                chapter_id = chapterId,
                action_type = ActionPlotManager,
                model_name = model,
                prompt_tokens = 0,
                completion_tokens = 0,
                total_tokens = 0,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });
        }
        catch (JsonException)
        {
            // Ignore parse errors; memory update is best-effort
        }

        if (reIndexRagAfter)
            await _ragService.TryEnsureIndexedAsync(storyId, null, cancellationToken);
    }
}
