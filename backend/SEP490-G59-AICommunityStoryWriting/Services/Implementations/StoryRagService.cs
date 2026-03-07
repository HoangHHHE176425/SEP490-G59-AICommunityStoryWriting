using System.Text.Json;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using Repositories;
using Repositories.Interfaces;
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
    private readonly IStoryChapterChunkRepository _chunkRepository;
    private readonly IVectorStore? _vectorStore;
    private readonly IConfiguration _configuration;

    public StoryRagService(
        IStoryRepository storyRepository,
        IChapterRepository chapterRepository,
        IStoryChapterChunkRepository chunkRepository,
        IConfiguration configuration,
        IVectorStore? vectorStore = null)
    {
        _storyRepository = storyRepository;
        _chapterRepository = chapterRepository;
        _chunkRepository = chunkRepository;
        _vectorStore = vectorStore;
        _configuration = configuration;
    }

    private bool UseVectorStore => _vectorStore != null && _configuration["VectorStore:Provider"]?.Equals("FAISS", StringComparison.OrdinalIgnoreCase) == true;

    public bool IsRagAvailableForStory(Guid storyId)
    {
        if (EmbeddingHelper.GetEmbeddingConfig(_configuration) == null)
            return false;
        if (UseVectorStore)
            return _vectorStore!.HasIndex(storyId) && _chunkRepository.CountByStoryId(storyId) > 0;
        var withEmbeddings = _chunkRepository.GetByStoryIdWithEmbeddings(storyId);
        return withEmbeddings.Count > 0;
    }

    public async Task EnsureIndexedAsync(Guid storyId, Guid? afterChapterId, CancellationToken cancellationToken = default)
    {
        var config = EmbeddingHelper.GetEmbeddingConfig(_configuration);
        if (config == null)
            return;

        var (baseUrl, apiKey, model) = config.Value;
        var chapters = _chapterRepository.GetByStoryId(storyId)
            .OrderBy(c => c.order_index)
            .ToList();

        IEnumerable<chapters> toIndex = chapters;
        if (afterChapterId.HasValue)
        {
            var idx = chapters.FirstOrDefault(c => c.id == afterChapterId.Value)?.order_index;
            if (idx.HasValue)
                toIndex = chapters.Where(c => c.order_index <= idx.Value);
        }

        if (UseVectorStore)
            _vectorStore!.DeleteStory(storyId);
        _chunkRepository.DeleteByStoryId(storyId);

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

        var chunksToAdd = new List<story_chapter_chunks>();
        var allVectorsForFaiss = new List<float[]>();
        int chunkIndex = 0;
        for (int i = 0; i < orderedItems.Count; i += batchSize)
        {
            var batch = orderedItems.Skip(i).Take(batchSize).ToList();
            var texts = batch.Select(x => x.text).ToArray();
            var embeddings = await EmbeddingHelper.GetEmbeddingsBatchAsync(texts, baseUrl, apiKey, model, cancellationToken);
            for (int j = 0; j < batch.Count && j < embeddings.Count; j++)
            {
                var (text, chapterId) = batch[j];
                var chunkId = Guid.NewGuid();
                chunksToAdd.Add(new story_chapter_chunks
                {
                    id = chunkId,
                    story_id = storyId,
                    chapter_id = chapterId,
                    chunk_index = chunkIndex++,
                    content = text,
                    embedding_json = UseVectorStore ? null : JsonSerializer.Serialize(embeddings[j]),
                    embedding_model = model,
                    created_at = DateTime.UtcNow
                });
                if (UseVectorStore)
                    allVectorsForFaiss.Add(embeddings[j]);
            }
            if (delayBetweenBatchesMs > 0 && i + batchSize < orderedItems.Count)
                await Task.Delay(delayBetweenBatchesMs, cancellationToken);
        }

        if (chunksToAdd.Count > 0)
        {
            _chunkRepository.AddRange(chunksToAdd);
            if (UseVectorStore && allVectorsForFaiss.Count == chunksToAdd.Count)
                _vectorStore!.AddVectors(storyId, chunksToAdd.Select(c => c.id).ToList(), allVectorsForFaiss);
        }
    }

    public async Task TryEnsureIndexedAsync(Guid storyId, Guid? afterChapterId, CancellationToken cancellationToken = default)
    {
        if (EmbeddingHelper.GetEmbeddingConfig(_configuration) == null)
            return;
        if (UseVectorStore && _vectorStore!.HasIndex(storyId) && _chunkRepository.CountByStoryId(storyId) > 0)
            return;
        if (!UseVectorStore && _chunkRepository.CountByStoryId(storyId) > 0)
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
        var chapters = _chapterRepository.GetByStoryId(storyId).OrderBy(c => c.order_index).ToList();
        var chapterMap = chapters.ToDictionary(c => c.id, c => (c.order_index, c.title ?? $"Chương {c.order_index}"));

        List<story_chapter_chunks> scoredChunks;
        if (UseVectorStore)
        {
            if (!_vectorStore!.HasIndex(storyId))
                return null;
            var searchResults = _vectorStore.Search(storyId, queryEmbedding, topK);
            if (searchResults.Count == 0)
                return null;
            var chunkIds = searchResults.Select(r => r.ChunkId).ToList();
            var chunksById = _chunkRepository.GetChunksByIds(chunkIds).ToDictionary(c => c.id);
            scoredChunks = searchResults
                .Select(r => (chunk: chunksById.GetValueOrDefault(r.ChunkId), score: r.Score))
                .Where(x => x.chunk != null)
                .OrderByDescending(x => x.score)
                .Select(x => x.chunk!)
                .Take(topK)
                .ToList();
        }
        else
        {
            var chunks = _chunkRepository.GetByStoryIdWithEmbeddings(storyId);
            if (chunks.Count == 0)
                return null;
            scoredChunks = chunks
                .Select(c => (chunk: c, score: CosineSimilarity(ParseEmbedding(c.embedding_json), queryEmbedding)))
                .OrderByDescending(x => x.score)
                .Take(topK)
                .Select(x => x.chunk)
                .ToList();
        }

        var lines = new List<string>();
        int totalChars = 0;
        foreach (var chunk in scoredChunks)
        {
            if (totalChars >= maxChars)
                break;
            var (order, title) = chapterMap.GetValueOrDefault(chunk.chapter_id, (0, ""));
            var block = $"[Chương {order}: {title}]\n{chunk.content}";
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

    private static float[] ParseEmbedding(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<float>();
        try
        {
            return JsonSerializer.Deserialize<float[]>(json) ?? Array.Empty<float>();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length != a.Length)
            return 0f;
        float dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        var denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
        return denom < 1e-9f ? 0f : dot / denom;
    }
}
