using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using Repositories;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

public class StoryRagService : IStoryRagService
{
    private const int ChunkSizeChars = 450;
    private const int ChunkOverlapChars = 80;
    private const int DefaultEmbeddingBatchSize = 20;
    private const int DefaultDelayBetweenBatchesMs = 500;

    private readonly IStoryRepository _storyRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IVectorStore _vectorStore;
    private readonly IConfiguration _configuration;

    public StoryRagService(
        IStoryRepository storyRepository,
        IChapterRepository chapterRepository,
        IConfiguration configuration,
        IVectorStore vectorStore)
    {
        _storyRepository = storyRepository;
        _chapterRepository = chapterRepository;
        _configuration = configuration;
        _vectorStore = vectorStore;
    }

    public bool IsRagAvailableForStory(Guid storyId)
    {
        if (EmbeddingHelper.GetEmbeddingConfig(_configuration) == null)
            return false;
        return _vectorStore.HasIndex(storyId) && _vectorStore.GetChunkCount(storyId) > 0;
    }

    public RagStatusResponse GetRagStatus(Guid storyId)
    {
        var embeddingConfigured = EmbeddingHelper.GetEmbeddingConfig(_configuration) != null;
        var chunkCount = _vectorStore.GetChunkCount(storyId);
        var hasVectorIndex = _vectorStore.HasIndex(storyId);
        var available = IsRagAvailableForStory(storyId);
        return new RagStatusResponse
        {
            StoryId = storyId,
            Available = available,
            EmbeddingConfigured = embeddingConfigured,
            ChunkCount = chunkCount,
            HasVectorIndex = hasVectorIndex
        };
    }

    public async Task EnsureIndexedAsync(Guid storyId, Guid? afterChapterId, CancellationToken cancellationToken = default)
    {
        var config = EmbeddingHelper.GetEmbeddingConfig(_configuration);
        if (config == null)
            return;

        var (baseUrl, apiKey, model) = config.Value;
        // Mốc thứ tự có thể là chương DRAFT (FE gửi "chương liền trước"); chỉ embed nội dung chương PUBLISHED.
        var allChapters = _chapterRepository.GetByStoryId(storyId).OrderBy(c => c.order_index).ToList();
        var publishedChapters = _chapterRepository.GetPublishedByStoryId(storyId).ToList();

        var indexedChapterIds = _vectorStore.GetIndexedChapterIds(storyId).ToHashSet();
        bool hasExistingIndex = indexedChapterIds.Count > 0;
        bool doIncremental = hasExistingIndex && !afterChapterId.HasValue;

        List<chapters> toIndex;
        if (doIncremental)
        {
            toIndex = publishedChapters.Where(c => !indexedChapterIds.Contains(c.id)).ToList();
            if (toIndex.Count == 0)
                return;
        }
        else
        {
            if (afterChapterId.HasValue)
            {
                var idx = allChapters.FirstOrDefault(c => c.id == afterChapterId.Value)?.order_index;
                toIndex = idx.HasValue
                    ? publishedChapters.Where(c => c.order_index <= idx.Value).ToList()
                    : publishedChapters;
            }
            else
                toIndex = publishedChapters.ToList();

            _vectorStore.DeleteStory(storyId);
        }

        int batchSize = _configuration.GetValue("AI:EmbeddingBatchSize", DefaultEmbeddingBatchSize);
        if (batchSize < 1) batchSize = 1;
        if (batchSize > 100) batchSize = 100;
        int delayBetweenBatchesMs = _configuration.GetValue("AI:EmbeddingDelayBetweenBatchesMs", DefaultDelayBetweenBatchesMs);
        if (delayBetweenBatchesMs < 0) delayBetweenBatchesMs = 0;

        var orderedItems = new List<(string text, Guid chapterId)>();
        foreach (var ch in toIndex)
        {
            var content = ch.content ?? "";
            foreach (var block in SplitIntoChunks(content))
            {
                if (string.IsNullOrWhiteSpace(block)) continue;
                orderedItems.Add((block, ch.id));
            }
        }

        var chunkIds = new List<Guid>();
        var chapterIds = new List<Guid>();
        var allVectors = new List<float[]>();
        var allContents = new List<string>();

        for (int i = 0; i < orderedItems.Count; i += batchSize)
        {
            var batch = orderedItems.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(x => x.text).ToArray();
            var embeddings = await EmbeddingHelper.GetEmbeddingsBatchAsync(texts, baseUrl, apiKey, model, cancellationToken);
            for (int j = 0; j < batch.Count && j < embeddings.Count; j++)
            {
                var (text, chapterId) = batch[j];
                chunkIds.Add(Guid.NewGuid());
                chapterIds.Add(chapterId);
                allVectors.Add(embeddings[j]);
                allContents.Add(text);
            }
            if (delayBetweenBatchesMs > 0 && i + batchSize < orderedItems.Count)
                await Task.Delay(delayBetweenBatchesMs, cancellationToken);
        }

        if (chunkIds.Count > 0)
        {
            var newIndexedChapterIds = toIndex.Select(c => c.id).Distinct().ToList();
            if (doIncremental)
            {
                var (existingIds, existingChapterIds, existingVectors, existingContents) = _vectorStore.GetIdsVectorsAndContents(storyId);
                var combinedIds = existingIds.Concat(chunkIds).ToList();
                var combinedChapterIds = existingChapterIds.Concat(chapterIds).ToList();
                var combinedVectors = existingVectors.Concat(allVectors).ToList();
                var combinedContents = existingContents.Concat(allContents).ToList();
                var combinedIndexedChapters = indexedChapterIds.Union(newIndexedChapterIds).Distinct().ToList();
                _vectorStore.AddVectors(storyId, combinedIds, combinedChapterIds, combinedVectors, combinedContents, combinedIndexedChapters);
            }
            else
                _vectorStore.AddVectors(storyId, chunkIds, chapterIds, allVectors, allContents, newIndexedChapterIds);
        }
    }

    public async Task TryEnsureIndexedAsync(Guid storyId, Guid? afterChapterId, CancellationToken cancellationToken = default)
    {
        if (EmbeddingHelper.GetEmbeddingConfig(_configuration) == null)
            return;
        if (_vectorStore.HasIndex(storyId) && _vectorStore.GetChunkCount(storyId) > 0)
            return;
        await EnsureIndexedAsync(storyId, afterChapterId, cancellationToken);
    }

    public async Task<string?> RetrieveContextAsync(Guid storyId, string query, int maxChars = 12000, int topK = 20, CancellationToken cancellationToken = default)
    {
        var config = EmbeddingHelper.GetEmbeddingConfig(_configuration);
        if (config == null)
            return null;

        var (baseUrl, apiKey, model) = config.Value;
        var queryEmbedding = await EmbeddingHelper.GetEmbeddingAsync(query, baseUrl, apiKey, model, cancellationToken);
        var publishedChapters = _chapterRepository.GetPublishedByStoryId(storyId).ToList();
        var chapterMap = publishedChapters.ToDictionary(c => c.id, c => (c.order_index, c.title ?? $"Chương {c.order_index}"));
        var publishedChapterIds = chapterMap.Keys.ToHashSet();

        if (!_vectorStore.HasIndex(storyId))
            return null;
        var searchResults = _vectorStore.Search(storyId, queryEmbedding, topK);
        if (searchResults.Count == 0)
            return null;
        var chunkIds = searchResults.Select(r => r.ChunkId).ToList();
        var infos = _vectorStore.GetChunkInfos(storyId, chunkIds);
        var lines = new List<string>();
        int totalChars = 0;
        foreach (var (_, chapterId, content) in infos.OrderBy(x => chunkIds.IndexOf(x.ChunkId)))
        {
            if (!publishedChapterIds.Contains(chapterId))
                continue;
            if (totalChars >= maxChars)
                break;
            var (order, title) = chapterMap.GetValueOrDefault(chapterId, (0, ""));
            var block = $"[Chương {order}: {title}]\n{content}";
            if (totalChars + block.Length > maxChars)
                block = block[..(maxChars - totalChars)] + "...";
            lines.Add(block);
            totalChars += block.Length;
        }

        return lines.Count == 0 ? null : string.Join("\n\n", lines);
    }

    private static List<string> SplitIntoChunks(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return new List<string>();

        var chunks = new List<string>();
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);
        var current = new List<string>();
        int currentLen = 0;

        foreach (var p in paragraphs)
        {
            if (currentLen + p.Length + 2 > ChunkSizeChars && current.Count > 0)
            {
                chunks.Add(string.Join("\n\n", current));
                var joined = string.Join("\n\n", current);
                var overlapChars = joined.Length > ChunkOverlapChars ? joined.Substring(joined.Length - ChunkOverlapChars) : joined;
                current = new List<string> { overlapChars };
                currentLen = current[0].Length;
            }
            current.Add(p);
            currentLen += p.Length + 2;
        }

        if (current.Count > 0)
            chunks.Add(string.Join("\n\n", current));

        if (chunks.Count == 0 && text.Length > 0)
        {
            for (int i = 0; i < text.Length; i += ChunkSizeChars - ChunkOverlapChars)
            {
                var len = Math.Min(ChunkSizeChars, text.Length - i);
                chunks.Add(text.Substring(i, len));
            }
        }

        return chunks;
    }
}
