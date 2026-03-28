using System.Text.Json;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories;
using Repositories.Interfaces;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Agent phân tích chương đã lưu (xuất bản), trích xuất nhân vật / sự kiện / trạng thái truyện và ghi DB.</summary>
public class ChapterMemoryAnalysisService : IChapterMemoryAnalysisService
{
    private const string ActionType = "CHAPTER_MEMORY_ANALYSIS";

    private readonly IConfiguration _configuration;
    private readonly IStoryRepository _storyRepository;
    private readonly IStoryCharacterMemoryRepository _characterMemoryRepository;
    private readonly IStoryEventMemoryRepository _eventMemoryRepository;
    private readonly IStoryStoryStateRepository _storyStateRepository;
    private readonly IAIUsageLogRepository _aiUsageLogRepository;

    public ChapterMemoryAnalysisService(
        IConfiguration configuration,
        IStoryRepository storyRepository,
        IStoryCharacterMemoryRepository characterMemoryRepository,
        IStoryEventMemoryRepository eventMemoryRepository,
        IStoryStoryStateRepository storyStateRepository,
        IAIUsageLogRepository aiUsageLogRepository)
    {
        _configuration = configuration;
        _storyRepository = storyRepository;
        _characterMemoryRepository = characterMemoryRepository;
        _eventMemoryRepository = eventMemoryRepository;
        _storyStateRepository = storyStateRepository;
        _aiUsageLogRepository = aiUsageLogRepository;
    }

    public async Task ExtractAndPersistAsync(
        Guid storyId,
        Guid chapterId,
        string chapterTitle,
        int orderIndex,
        string chapterContent,
        CancellationToken cancellationToken = default)
    {
        if (!_configuration.GetValue("AI:EnableChapterMemoryAnalysis", true))
            return;

        var maxChars = _configuration.GetValue("AI:ChapterMemoryAnalysisMaxInputChars", 14000);
        if (maxChars < 2000) maxChars = 14000;

        var content = chapterContent.Trim();
        if (content.Length > maxChars)
            content = content[..maxChars] + "\n[... nội dung bị cắt cho phân tích ...]";

        var story = _storyRepository.GetById(storyId);
        var storyTitle = story?.title ?? "";

        var existingCharacters = _characterMemoryRepository.GetByStoryId(storyId);
        var existingState = _storyStateRepository.GetByStoryId(storyId);

        var memoryContext = BuildExistingMemoryPromptBlock(existingCharacters, existingState);

        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentMemoryAnalyzer);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

        var userPrompt =
            $"Truyện: {storyTitle}\n" +
            $"Chương (order_index={orderIndex}): {chapterTitle}\n\n" +
            $"{memoryContext}\n\n" +
            "=== NỘI DUNG CHƯƠNG (phân tích chương này) ===\n" +
            content;

        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt()),
            new UserChatMessage(userPrompt)
        };

        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentMemoryAnalyzer);

        ChatCompletion completion;
        try
        {
            completion = await client.CompleteChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = story?.author_id,
                story_id = storyId,
                chapter_id = chapterId,
                action_type = ActionType,
                model_name = model,
                status = "FAILED: " + ex.Message[..Math.Min(200, ex.Message.Length)],
                created_at = DateTime.UtcNow
            });
            throw;
        }

        var text = completion.Content?.Count > 0 ? completion.Content[0].Text : null;
        if (string.IsNullOrWhiteSpace(text))
        {
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = story?.author_id,
                story_id = storyId,
                chapter_id = chapterId,
                action_type = ActionType,
                model_name = model,
                status = "EMPTY_RESPONSE",
                created_at = DateTime.UtcNow
            });
            return;
        }

        var promptTokens = completion.Usage?.InputTokenCount ?? 0;
        var completionTokens = completion.Usage?.OutputTokenCount ?? 0;

        try
        {
            ApplyExtractionJson(storyId, chapterId, UnwrapJsonFromMarkdown(text.Trim()));
        }
        catch (JsonException ex)
        {
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = story?.author_id,
                story_id = storyId,
                chapter_id = chapterId,
                action_type = ActionType,
                model_name = model,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                status = "PARSE_ERROR: " + ex.Message[..Math.Min(180, ex.Message.Length)],
                created_at = DateTime.UtcNow
            });
            return;
        }

        _aiUsageLogRepository.Log(new ai_usage_logs
        {
            user_id = story?.author_id,
            story_id = storyId,
            chapter_id = chapterId,
            action_type = ActionType,
            model_name = model,
            prompt_tokens = promptTokens,
            completion_tokens = completionTokens,
            total_tokens = promptTokens + completionTokens,
            status = "SUCCESS",
            created_at = DateTime.UtcNow
        });
    }

    private static string BuildExistingMemoryPromptBlock(
        IReadOnlyList<story_character_memory> characters,
        story_story_state? state)
    {
        var parts = new List<string>();
        if (characters.Count > 0)
        {
            var lines = characters.Select(c =>
                $"- {c.character_name}: {(string.IsNullOrEmpty(c.state_json) ? "(chưa mô tả)" : c.state_json)}");
            parts.Add("=== Character Memory hiện có (cập nhật / bổ sung sau chương này) ===\n" + string.Join("\n", lines));
        }
        else
            parts.Add("=== Character Memory hiện có: (trống) ===");

        if (!string.IsNullOrWhiteSpace(state?.state_snapshot_json))
            parts.Add("=== Story State hiện có (JSON — gộp với chương này) ===\n" + state.state_snapshot_json);
        else
            parts.Add("=== Story State hiện có: (trống) ===");

        return string.Join("\n\n", parts);
    }

    private void ApplyExtractionJson(Guid storyId, Guid chapterId, string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (root.TryGetProperty("characters", out var ch) && ch.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in ch.EnumerateArray())
            {
                var name = el.TryGetProperty("name", out var n) ? n.GetString()?.Trim() : null;
                if (string.IsNullOrEmpty(name)) continue;
                string? summary = null;
                if (el.TryGetProperty("stateSummary", out var s))
                    summary = s.ValueKind == JsonValueKind.String ? s.GetString()?.Trim() : s.GetRawText().Trim();
                _characterMemoryRepository.Upsert(storyId, name, string.IsNullOrWhiteSpace(summary) ? null : summary);
            }
        }

        _eventMemoryRepository.DeleteByChapterId(chapterId);
        if (root.TryGetProperty("events", out var ev) && ev.ValueKind == JsonValueKind.Array)
        {
            var idx = 0;
            foreach (var el in ev.EnumerateArray())
            {
                string? desc = null;
                if (el.ValueKind == JsonValueKind.String)
                    desc = el.GetString();
                else if (el.ValueKind == JsonValueKind.Object && el.TryGetProperty("description", out var d))
                    desc = d.GetString();
                desc = desc?.Trim();
                if (string.IsNullOrEmpty(desc)) continue;
                if (desc.Length > 4000) desc = desc[..4000];
                _eventMemoryRepository.Add(storyId, chapterId, idx++, desc);
            }
        }

        if (root.TryGetProperty("storyState", out var ss) &&
            (ss.ValueKind == JsonValueKind.Object || ss.ValueKind == JsonValueKind.Array))
        {
            var raw = ss.GetRawText();
            if (!string.IsNullOrWhiteSpace(raw))
                _storyStateRepository.Upsert(storyId, raw);
        }
    }

    private static string UnwrapJsonFromMarkdown(string raw)
    {
        var t = raw.Trim();
        if (t.StartsWith("```", StringComparison.Ordinal))
        {
            var start = t.IndexOf('\n');
            if (start >= 0)
            {
                start++;
                var fenceEnd = t.IndexOf("```", start, StringComparison.Ordinal);
                if (fenceEnd > start) t = t[start..fenceEnd].Trim();
            }
        }

        var objStart = t.IndexOf('{');
        var objEnd = t.LastIndexOf('}');
        if (objStart >= 0 && objEnd > objStart)
            return t[objStart..(objEnd + 1)];
        return t;
    }

    private static string GetSystemPrompt() => """
Role: Bạn là agent trích xuất bộ nhớ truyện (Memory Extractor) sau khi đọc MỘT chương.

Nhiệm vụ:
1) Đọc nội dung chương và (nếu có) Character Memory + Story State đã cho.
2) Trả về DUY NHẤT một JSON hợp lệ (không markdown, không giải thích ngoài JSON).

Schema bắt buộc:
{
  "characters": [
    { "name": "Tên nhân vật đúng như trong truyện", "stateSummary": "Chuỗi mô tả ngắn gọn hoặc JSON nhỏ: trạng thái SAU chương này (sống/chết/mất tích, vị trí, mối quan hệ chính, mục tiêu)." }
  ],
  "events": [
    { "description": "Một sự kiện quan trọng xảy ra trong chương này, thứ tự thời gian trong chương." }
  ],
  "storyState": { }
}

Quy tắc:
- characters: chỉ những nhân vật xuất hiện hoặc được cập nhật rõ ràng trong chương; cập nhật stateSummary để phản ánh hậu quả của chương (không bịa thêm ngoài văn bản).
- events: 3–12 mục nếu chương đủ dài; chương rất ngắn có thể ít hơn; mỗi description một dòng ý, tiếng Việt nếu truyện tiếng Việt.
- storyState: một object JSON tổng hợp trạng thái câu chuyện SAU chương (địa điểm hiện tại, xung đột mở, phe, thời điểm nếu có). Gộp logic từ Story State cũ (nếu có) với chương này.
- Không dùng nhãn markdown; không bọc ```.
""";
}
