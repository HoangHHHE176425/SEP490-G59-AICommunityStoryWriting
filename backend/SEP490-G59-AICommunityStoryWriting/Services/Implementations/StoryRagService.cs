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
    private const int DefaultEmbeddingQueryMaxChars = 10000;

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

    public async Task EnsureIndexedAsync(Guid storyId, Guid? upToChapterId, CancellationToken cancellationToken = default)
    {
        var config = EmbeddingHelper.GetEmbeddingConfig(_configuration);
        if (config == null)
            return;

        var (baseUrl, apiKey, model) = config.Value;
        //Lấy toàn bộ chương truyện,sắp xếp theo thứ tự chương
        //Lấy riêng danh sách chương đã publish
        var allChapters = _chapterRepository.GetByStoryId(storyId).OrderBy(c => c.order_index).ToList();
        var publishedChapters = _chapterRepository.GetPublishedByStoryId(storyId).ToList();
        //Lấy ds chương đã được embedding (đã index) để nếu có index rồi thì chỉ cần index thêm các chương mới
        var indexedChapterIds = _vectorStore.GetIndexedChapterIds(storyId).ToHashSet();
        bool hasExistingIndex = indexedChapterIds.Count > 0;
        bool doIncremental = hasExistingIndex && !upToChapterId.HasValue;
        //xác định danh sách chương cần index: nếu id đã có chương 
        List<chapters> toIndex;
        if (doIncremental)
        {
            //chọn các chương đã publish mà chưa có trong index để index thêm
            toIndex = publishedChapters.Where(c => !indexedChapterIds.Contains(c.id)).ToList();
            if (toIndex.Count == 0)
                return;
        }
        else
        {
            if (upToChapterId.HasValue)
            {
                //tìm order_index của chương mốc, chọn các chương đã publish có order_index <= chương mốc trong allChapters để index
                var idx = allChapters.FirstOrDefault(c => c.id == upToChapterId.Value)?.order_index;
                //nếu không tìm thấy chương mốc thì index tất cả chương đã publish, nếu tìm thấy thì chỉ index các chương đã publish có order_index <= chương mốc   
                toIndex = idx.HasValue
                    ? publishedChapters.Where(c => c.order_index <= idx.Value).ToList()
                    : publishedChapters;
            }
            else
                //nếu không có chương mốc thì index tất cả chương đã publish
                toIndex = publishedChapters.ToList();
            //Xóa để ghi mới
            _vectorStore.DeleteStory(storyId);
        }

        int batchSize = _configuration.GetValue("AI:EmbeddingBatchSize", DefaultEmbeddingBatchSize);
        if (batchSize < 1) batchSize = 1;
        if (batchSize > 100) batchSize = 100;
        int delayBetweenBatchesMs = _configuration.GetValue("AI:EmbeddingDelayBetweenBatchesMs", DefaultDelayBetweenBatchesMs);
        if (delayBetweenBatchesMs < 0) delayBetweenBatchesMs = 0;
        //tạo danh sách item cần emb theo thứ tự
        var orderedItems = new List<(string text, Guid chapterId)>();
        //duyệt từng chương trong toIndex
        foreach (var ch in toIndex)
        {
            //chuẩn hóa nội dung chương
            var content = ChapterContentNormalizer.NormalizeForAi(ch.content, 0);
            //chia noi dung chương thành nhiều chunk 
            foreach (var block in SplitIntoChunks(content))
            {
                //bỏ chuỗi rỗng
                if (string.IsNullOrWhiteSpace(block)) continue;
                orderedItems.Add((block, ch.id));
            }
        }

        var chunkIds = new List<Guid>();//Id của từng chunk
        var chapterIds = new List<Guid>();//chapterId tương ứng với từng chunk để sau này lấy thông tin chương khi search
        var allVectors = new List<float[]>();//vector embedding tương ứng với từng chunk
        var allContents = new List<string>();//nội dung chunk tương ứng với từng vector
        //lặp theo batch trên toàn bộ chunk
        for (int i = 0; i < orderedItems.Count; i += batchSize)
        {   
            var batch = orderedItems.Skip(i).Take(batchSize).ToList();
            //lấy mảng text của batch để gọi embedding
            var texts = batch.Select(x => x.text).ToArray();
            var embeddings = await EmbeddingHelper.GetEmbeddingsBatchAsync(texts, baseUrl, apiKey, model, cancellationToken);
            //duyệt từng item trong batch, tạo id mới cho chunk, lưu chapterId, vector embedding và content vào list tương ứng
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
            //lấy danh sách chapterId đã được index trước đó và chapterId của các chunk mới để tạo danh sách chapterId đã được index sau khi thêm mới, lưu vào vector store
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

    public async Task TryEnsureIndexedAsync(Guid storyId, Guid? upToChapterId, CancellationToken cancellationToken = default)
    {
        if (EmbeddingHelper.GetEmbeddingConfig(_configuration) == null)
            return;
        if (_vectorStore.HasIndex(storyId) && _vectorStore.GetChunkCount(storyId) > 0)
            return;
        await EnsureIndexedAsync(storyId, upToChapterId, cancellationToken);
    }
    //Tìm và trả về các đoạn liên quan nhất với query (ý tưởng tác giả / nhân vật / sự kiện). Trả về text đã ghép để đưa vào prompt. Nếu RAG không dùng được thì trả về null.
    public async Task<string?> RetrieveContextAsync(Guid storyId, string query, int maxChars = 12000, int topK = 20, CancellationToken cancellationToken = default)
    {
        var config = EmbeddingHelper.GetEmbeddingConfig(_configuration);
        if (config == null)
            return null;

        var (baseUrl, apiKey, model) = config.Value;
        int maxQueryChars = _configuration.GetValue("AI:EmbeddingQueryMaxChars", DefaultEmbeddingQueryMaxChars);
        if (maxQueryChars < 256)
            maxQueryChars = DefaultEmbeddingQueryMaxChars;
        var q = query?.Trim() ?? "";
        if (q.Length > maxQueryChars)
            q = q[^maxQueryChars..];
        var queryEmbedding = await EmbeddingHelper.GetEmbeddingAsync(q, baseUrl, apiKey, model, cancellationToken);
        //lấy danh sách chương đã publish của truyện
        var publishedChapters = _chapterRepository.GetPublishedByStoryId(storyId).ToList();
        //tạo map từ chapterId sang (order_index, title) để sau này khi lấy thông tin chunk có chapterId thì biết được thứ tự chương và tiêu đề chương để build context, đồng thời tạo tập hợp các chapterId đã publish để lọc kết quả chunk sau này
        var chapterMap = publishedChapters.ToDictionary(c => c.id, c => (c.order_index, c.title ?? $"Chương {c.order_index}"));
        //tạo tập hợp các chapterId đã publish để lọc kết quả chunk sau này
        var publishedChapterIds = chapterMap.Keys.ToHashSet();

        if (!_vectorStore.HasIndex(storyId))
            return null;
        //tìm top-k chunk gần nhất với queryEmbedding (cosine similarity), lấy chunkId và điểm số, nếu không có kết quả nào thì trả về null
        var searchResults = _vectorStore.Search(storyId, queryEmbedding, topK);
        if (searchResults.Count == 0)
            return null;
        //lấy danh sách chunkId từ kết quả tìm kiếm, gọi vector store để lấy thông tin (chunkId, chapterId, content) cho các chunkId đó, sắp xếp theo thứ tự chunkId trong kết quả tìm kiếm để ưu tiên các chunk có điểm cao hơn
        var chunkIds = searchResults.Select(r => r.ChunkId).ToList();
        //đọc thông tin chi tiết của chunk(chunkId,chapterId,content)
        var infos = _vectorStore.GetChunkInfos(storyId, chunkIds);
        var lines = new List<string>();
        int totalChars = 0;
        //duyệt từng chunk info theo thứ tự chunkId trong kết quả tìm kiếm,bỏ qua chunkId trong tuple bằng _,chỉ dùng chapterId và content
        foreach (var (_, chapterId, content) in infos.OrderBy(x => chunkIds.IndexOf(x.ChunkId)))
        {
            //nếu chunk thuộc chương chưa publish thì bỏ qua
            if (!publishedChapterIds.Contains(chapterId))
                continue;
            if (totalChars >= maxChars)
                break;
            //lấy thứ tự chương + tiêu đề từ chapterMap,không có thì mặc định 0,"" 
            var (order, title) = chapterMap.GetValueOrDefault(chapterId, (0, ""));
            //tạo block gồm header chương + nội dung chunk
            var block = $"[Chương {order}: {title}]\n{content}";
            //nếu block vượt quá maxchar thì cắt bớt phần content để block vừa đúng maxchar,thêm "..." vào cuối block để đánh dấu đã bị cắt
            if (totalChars + block.Length > maxChars)
                block = block[..(maxChars - totalChars)] + "...";
            lines.Add(block);
            totalChars += block.Length;
        }
        //ghép các block thành một chuỗi duy nhất để trả về, nếu không có block nào thì trả về null
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
