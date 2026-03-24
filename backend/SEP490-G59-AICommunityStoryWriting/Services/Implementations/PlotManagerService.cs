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
    private readonly IAiGeneratedContentRepository _aiContentRepository;
    private readonly IConfiguration _configuration;

    public PlotManagerService(
        IStoryContextBuilder contextBuilder,
        IStoryCharacterMemoryRepository characterRepo,
        IStoryEventMemoryRepository eventRepo,
        IStoryStoryStateRepository stateRepo,
        IStoryRagService ragService,
        IStoryRepository storyRepository,
        IAIUsageLogRepository aiUsageLogRepository,
        IAiGeneratedContentRepository aiContentRepository,
        IConfiguration configuration)
    {
        _contextBuilder = contextBuilder;
        _characterRepo = characterRepo;
        _eventRepo = eventRepo;
        _stateRepo = stateRepo;
        _ragService = ragService;
        _storyRepository = storyRepository;
        _aiUsageLogRepository = aiUsageLogRepository;
        _aiContentRepository = aiContentRepository;
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

        // Lấy mô tả/ý tưởng của tác giả (nếu chapter được tạo từ co-author).
        // Dùng để xác định "nhân vật chính" theo mô tả của tác giả.
        var aiRecord = _aiContentRepository.GetLatestByChapterId(chapterId);
        var authorDescription = string.IsNullOrWhiteSpace(aiRecord?.input_prompt) ? null : aiRecord!.input_prompt!.Trim();

        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentPlotManager);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

        var systemPrompt = """
Bạn là Plot Manager: từ ngữ cảnh truyện và nội dung chương mới, trích xuất:
(1) sự kiện mới cho timeline,
(2) danh sách nhân vật CHÍNH theo mô tả tác giả (nếu có),
(3) cập nhật trạng thái nhân vật,
(4) cập nhật story state (thế giới, quy tắc).

Trả về DUY NHẤT một JSON hợp lệ, không markdown:
{
  "timelineUpdates": [ { "description": "Mô tả ngắn sự kiện" } ],
  "mainCharacters": [ "Tên nhân vật theo context" ],
  "characterStateUpdates": [ { "characterName": "Tên nhân vật", "stateJson": "Mô tả trạng thái/tính cách" } ],
  "storyStateUpdate": "Mô tả ngắn trạng thái truyện / quy tắc thế giới (hoặc null nếu không đổi)"
}
Quy tắc nhân vật CHÍNH:
- Nếu có Author description: mainCharacters phải là các nhân vật được tác giả nhắc đến như trọng tâm/nhân vật chính trong chương tiếp theo.
- Nếu không có Author description: mainCharacters = [].

Quy tắc cập nhật Character Memory:
- Chỉ được đưa vào characterStateUpdates các nhân vật thuộc mainCharacters HOẶC các nhân vật có state "durable" thay đổi lâu dài (đã chết/đã qua đời/mất tích/đã rời đi/đang bị bắt/khống chế/không còn xuất hiện).

Quan trọng: Với nhân vật đã chết, đã qua đời, hoặc đã rời đi/mất tích trong chương, bắt buộc ghi rõ vào characterStateUpdates (vd. stateJson: "đã chết", "đã qua đời", "đã rời đi").
Nếu không có gì mới: timelineUpdates/characterStateUpdates dùng [], mainCharacters = [], storyStateUpdate null.
""";

        var userPrompt =
            $"Ngữ cảnh truyện (các chương trước + tóm tắt):\n\n{contextBlock}\n\n---\n" +
            $"Author description (ý tưởng/gợi ý tác giả, có thể null):\n{(authorDescription ?? "(null)")}\n\n---\n" +
            $"Nội dung chương mới cần trích xuất:\n\n{chapterContent}\n\n{languageInstruction}\n\n" +
            "Trả về JSON theo đúng cấu trúc trên.";

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(systemPrompt),
            new UserChatMessage(userPrompt)
        };

        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentPlotManager);
        var completion = await client.CompleteChatAsync(messages, options);
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

            var mainCharacters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (root.TryGetProperty("mainCharacters", out var mc) && mc.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in mc.EnumerateArray())
                {
                    var n = item.GetString();
                    if (!string.IsNullOrWhiteSpace(n))
                        mainCharacters.Add(n.Trim());
                }
            }

            if (root.TryGetProperty("characterStateUpdates", out var csu) && csu.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in csu.EnumerateArray())
                {
                    var name = item.TryGetProperty("characterName", out var n) ? n.GetString() : null;
                    var state = item.TryGetProperty("stateJson", out var s) ? s.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    var trimmedName = name.Trim();
                    var trimmedState = state?.Trim();

                    // Filter: chỉ lưu nhân vật chính theo mô tả tác giả hoặc state durable.
                    if (mainCharacters.Count == 0)
                    {
                        if (IsDurableState(trimmedState))
                            _characterRepo.Upsert(storyId, trimmedName, trimmedState);
                    }
                    else
                    {
                        if (mainCharacters.Contains(trimmedName) || IsDurableState(trimmedState))
                            _characterRepo.Upsert(storyId, trimmedName, trimmedState);
                    }
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
            await _ragService.EnsureIndexedAsync(storyId, null, cancellationToken);
    }

    private static bool IsDurableState(string? stateJson)
    {
        if (string.IsNullOrWhiteSpace(stateJson)) return false;
        var s = stateJson.Trim();
        // Heuristic: từ khóa biểu thị thay đổi lâu dài.
        return s.Contains("đã chết", StringComparison.OrdinalIgnoreCase)
               || s.Contains("đã qua đời", StringComparison.OrdinalIgnoreCase)
               || s.Contains("mất tích", StringComparison.OrdinalIgnoreCase)
               || s.Contains("đã rời đi", StringComparison.OrdinalIgnoreCase)
               || s.Contains("rời đi", StringComparison.OrdinalIgnoreCase)
               || s.Contains("biến mất", StringComparison.OrdinalIgnoreCase)
               || s.Contains("dead", StringComparison.OrdinalIgnoreCase)
               || s.Contains("missing", StringComparison.OrdinalIgnoreCase)
               || s.Contains("captured", StringComparison.OrdinalIgnoreCase)
               || s.Contains("không còn xuất hiện", StringComparison.OrdinalIgnoreCase);
    }
}
