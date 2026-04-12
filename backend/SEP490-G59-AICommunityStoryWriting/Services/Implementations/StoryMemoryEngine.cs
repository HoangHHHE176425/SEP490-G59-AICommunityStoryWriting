using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using Repositories;
using Repositories.Interfaces;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Story Memory Engine: ghép Story Context (chỉ RAG) + Character Memory + Event Memory + Story State. Không dùng Story Memory (N chương) — chỉ RAG để chất lượng tối đa.</summary>
public class StoryMemoryEngine : IStoryMemoryEngine
{
    private const int DefaultRagMaxChars = 12000;
    private const int DefaultRagTopK = 20;

    private readonly IStoryCharacterMemoryRepository _characterRepo;
    private readonly IStoryEventMemoryRepository _eventRepo;
    private readonly IStoryStoryStateRepository _stateRepo;
    private readonly IStoryRagService _ragService;
    private readonly IStoryRepository _storyRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IConfiguration _configuration;

    public StoryMemoryEngine(
        IStoryCharacterMemoryRepository characterRepo,
        IStoryEventMemoryRepository eventRepo,
        IStoryStoryStateRepository stateRepo,
        IStoryRagService ragService,
        IStoryRepository storyRepository,
        IChapterRepository chapterRepository,
        IConfiguration configuration)
    {
        _characterRepo = characterRepo;
        _eventRepo = eventRepo;
        _stateRepo = stateRepo;
        _ragService = ragService;
        _storyRepository = storyRepository;
        _chapterRepository = chapterRepository;
        _configuration = configuration;
    }

    public async Task<string> BuildContextForCoCreateAsync(Guid storyId, string authorIdea, CancellationToken cancellationToken = default)
    {
        var story = _storyRepository.GetById(storyId);
        if (story == null)
            return string.Empty;

        var ragStatusCo = _ragService.GetRagStatus(storyId);
        if (!ragStatusCo.EmbeddingConfigured)
            throw new InvalidOperationException(
                "Chưa cấu hình embedding: AI:EmbeddingBaseUrl, AI:EmbeddingModel và API key (AI:ApiKey hoặc AI:EmbeddingApiKey). Xem thông báo tương tự khi gợi ý chương.");
        if (!_ragService.IsRagAvailableForStory(storyId))
            throw new InvalidOperationException("Truyện chưa được index RAG. Gọi POST /api/ai/index-rag sau khi đã cấu hình embedding và có chương PUBLISHED có nội dung.");

        int ragMaxChars = _configuration.GetValue("AI:CoCreateRagMaxChars", DefaultRagMaxChars);
        int ragTopK = _configuration.GetValue("AI:CoCreateRagTopK", DefaultRagTopK);
        if (ragMaxChars < 1000) ragMaxChars = DefaultRagMaxChars;
        if (ragTopK < 5) ragTopK = 5;
        var query = authorIdea.Trim();
        var ragBlock = await _ragService.RetrieveContextAsync(storyId, query, maxChars: ragMaxChars, topK: ragTopK, cancellationToken);

        if (string.IsNullOrWhiteSpace(ragBlock))
            throw new InvalidOperationException("Không lấy được ngữ cảnh từ RAG. Đảm bảo truyện đã có chương có nội dung và đã gọi POST /api/ai/index-rag.");

        var storyContextBlock = BuildRagStoryBlock(story, ragBlock);

        var characterBlock = BuildCharacterMemoryBlock(storyId);
        var eventBlock = BuildEventMemoryBlock(storyId);
        var stateBlock = BuildStoryStateBlock(storyId);

        var parts = new List<string> { storyContextBlock };
        if (!string.IsNullOrWhiteSpace(characterBlock)) parts.Add(characterBlock);
        if (!string.IsNullOrWhiteSpace(eventBlock)) parts.Add(eventBlock);
        if (!string.IsNullOrWhiteSpace(stateBlock)) parts.Add(stateBlock);
        parts.Add("## Ý tưởng tác giả");
        parts.Add(authorIdea.Trim());

        return string.Join("\n\n", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    /// <summary>Build context cho suggest-next-chapter: RAG (với ragQuery) + Character + Event + Story State. Không thêm ý tưởng tác giả.</summary>
    public async Task<string> BuildContextForSuggestAsync(Guid storyId, string ragQuery, CancellationToken cancellationToken = default)
    {
        var story = _storyRepository.GetById(storyId);
        if (story == null)
            return string.Empty;

        var ragStatusSuggest = _ragService.GetRagStatus(storyId);
        if (!ragStatusSuggest.EmbeddingConfigured)
            throw new InvalidOperationException(
                "Chưa cấu hình embedding: AI:EmbeddingBaseUrl, AI:EmbeddingModel và API key (AI:ApiKey hoặc AI:EmbeddingApiKey). Với OpenRouter: BaseUrl https://openrouter.ai/api/v1, model openai/text-embedding-3-small.");
        if (!_ragService.IsRagAvailableForStory(storyId))
            throw new InvalidOperationException("Truyện chưa được index RAG. Gọi POST /api/ai/index-rag (hoặc thử lại gợi ý) sau khi có chương PUBLISHED có nội dung.");

        int ragMaxChars = _configuration.GetValue("AI:CoCreateRagMaxChars", DefaultRagMaxChars);
        int ragTopK = _configuration.GetValue("AI:CoCreateRagTopK", DefaultRagTopK);
        if (ragMaxChars < 1000) ragMaxChars = DefaultRagMaxChars;
        if (ragTopK < 5) ragTopK = 5;
        var query = ragQuery?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(query))
            query = story.summary ?? story.title ?? "";
        var ragBlock = await _ragService.RetrieveContextAsync(storyId, query, maxChars: ragMaxChars, topK: ragTopK, cancellationToken);

        if (string.IsNullOrWhiteSpace(ragBlock))
            throw new InvalidOperationException("Không lấy được ngữ cảnh từ RAG. Đảm bảo truyện đã có chương có nội dung và đã index RAG.");

        var storyContextBlock = BuildRagStoryBlock(story, ragBlock);
        var characterBlock = BuildCharacterMemoryBlock(storyId);
        var eventBlock = BuildEventMemoryBlock(storyId);
        var stateBlock = BuildStoryStateBlock(storyId);

        var parts = new List<string> { storyContextBlock };
        if (!string.IsNullOrWhiteSpace(characterBlock)) parts.Add(characterBlock);
        if (!string.IsNullOrWhiteSpace(eventBlock)) parts.Add(eventBlock);
        if (!string.IsNullOrWhiteSpace(stateBlock)) parts.Add(stateBlock);
        return string.Join("\n\n", parts.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private static string BuildRagStoryBlock(stories story, string ragBlock)
    {
        var lines = new List<string>
        {
            $"## Truyện: {story.title}",
            string.IsNullOrWhiteSpace(story.summary) ? "" : $"Tóm tắt: {story.summary}",
            "## Các đoạn liên quan từ truyện (RAG)",
            ragBlock
        };
        return string.Join("\n\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private string BuildCharacterMemoryBlock(Guid storyId)
    {
        var list = _characterRepo.GetByStoryId(storyId);
        if (list.Count == 0) return string.Empty;
        var lines = list.Select(c => $"- {c.character_name}: {(string.IsNullOrEmpty(c.state_json) ? "(chưa mô tả)" : c.state_json)}");
        return "## Character Memory\n" + string.Join("\n", lines);
    }

    private string BuildEventMemoryBlock(Guid storyId)
    {
        var list = _eventRepo.GetByStoryId(storyId);
        if (list.Count == 0) return string.Empty;
        var publishedChapters = _chapterRepository.GetPublishedByStoryId(storyId).ToList();
        var chapterOrders = publishedChapters.ToDictionary(c => c.id, c => c.order_index);
        var publishedIds = chapterOrders.Keys.ToHashSet();
        var sorted = list
            .Where(e => e.chapter_id == null || (e.chapter_id is Guid ecid && publishedIds.Contains(ecid)))
            .OrderBy(e =>
            {
                if (e.chapter_id is Guid cid && chapterOrders.TryGetValue(cid, out var ord)) return ord;
                return int.MaxValue;
            })
            .ThenBy(e => e.order_index)
            .ThenBy(e => e.created_at ?? DateTime.MinValue)
            .ToList();
        if (sorted.Count == 0) return string.Empty;
        var lines = sorted.Select((e, i) => $"{i + 1}. {e.description}");
        return "## Event Memory (Timeline)\n" + string.Join("\n", lines);
    }

    private string BuildStoryStateBlock(Guid storyId)
    {
        var state = _stateRepo.GetByStoryId(storyId);
        if (state?.state_snapshot_json == null) return string.Empty;
        return "## Story State\n" + state.state_snapshot_json;
    }
}
