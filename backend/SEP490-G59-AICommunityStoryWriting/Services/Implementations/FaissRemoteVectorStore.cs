using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>
/// Remote FAISS vector store adapter.
/// Calls a dedicated FAISS HTTP service and keeps IVectorStore contract unchanged.
/// </summary>
public sealed class FaissRemoteVectorStore : IVectorStore
{
    private readonly HttpClient _http;
    private readonly ILogger<FaissRemoteVectorStore> _logger;
    private readonly int _maxRetries;
    private readonly int _retryDelayMs;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public FaissRemoteVectorStore(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<FaissRemoteVectorStore> logger)
    {
        _logger = logger;
        _http = httpClientFactory.CreateClient("FaissVectorStore");
        var baseUrl = (configuration["FaissService:BaseUrl"] ?? "http://127.0.0.1:8085").TrimEnd('/');
        _http.BaseAddress = new Uri(baseUrl);

        var timeoutSeconds = configuration.GetValue("FaissService:TimeoutSeconds", 60);
        if (timeoutSeconds < 1) timeoutSeconds = 15;
        _http.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        _maxRetries = configuration.GetValue("FaissService:MaxRetries", 2);
        if (_maxRetries < 0) _maxRetries = 0;
        if (_maxRetries > 5) _maxRetries = 5;
        _retryDelayMs = configuration.GetValue("FaissService:RetryDelayMs", 400);
        if (_retryDelayMs < 0) _retryDelayMs = 0;

        var apiKey = configuration["FaissService:ApiKey"];
        if (!string.IsNullOrWhiteSpace(apiKey))
        {
            _http.DefaultRequestHeaders.Remove("X-Api-Key");
            _http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        }
    }

    public bool HasIndex(Guid storyId)
    {
        var response = Send<GetHasIndexResponse>(HttpMethod.Get, $"/v1/index/has?storyId={storyId}");
        return response.HasIndex;
    }

    public void DeleteStory(Guid storyId)
    {
        SendNoContent(HttpMethod.Post, "/v1/index/delete-story", new StoryOnlyRequest { StoryId = storyId });
    }

    public void AddVectors(
        Guid storyId,
        IReadOnlyList<Guid> chunkIds,
        IReadOnlyList<Guid> chapterIds,
        IReadOnlyList<float[]> vectors,
        IReadOnlyList<string> contents,
        IReadOnlyList<Guid> indexedChapterIds)
    {
        var request = new UpsertRequest
        {
            StoryId = storyId,
            ChunkIds = chunkIds.ToList(),
            ChapterIds = chapterIds.ToList(),
            Vectors = vectors.Select(v => v.ToArray()).ToList(),
            Contents = contents.ToList(),
            IndexedChapterIds = indexedChapterIds.ToList()
        };

        SendNoContent(HttpMethod.Post, "/v1/index/upsert", request);
    }

    public IReadOnlyList<Guid> GetIndexedChapterIds(Guid storyId)
    {
        var response = Send<GetIndexedChaptersResponse>(HttpMethod.Get, $"/v1/index/indexed-chapters?storyId={storyId}");
        return response.IndexedChapterIds ?? new List<Guid>();
    }

    public int GetChunkCount(Guid storyId)
    {
        var response = Send<GetChunkCountResponse>(HttpMethod.Get, $"/v1/index/chunk-count?storyId={storyId}");
        return response.ChunkCount;
    }

    public IReadOnlyList<(Guid ChunkId, Guid ChapterId, string Content)> GetChunkInfos(Guid storyId, IReadOnlyList<Guid> chunkIds)
    {
        if (chunkIds == null || chunkIds.Count == 0)
            return Array.Empty<(Guid, Guid, string)>();

        var response = Send<GetChunkInfosResponse>(HttpMethod.Post, "/v1/index/chunk-infos", new GetChunkInfosRequest
        {
            StoryId = storyId,
            ChunkIds = chunkIds.ToList()
        });

        if (response.Items == null || response.Items.Count == 0)
            return Array.Empty<(Guid, Guid, string)>();

        return response.Items.Select(x => (x.ChunkId, x.ChapterId, x.Content ?? string.Empty)).ToList();
    }

    public (IReadOnlyList<Guid> Ids, IReadOnlyList<Guid> ChapterIds, IReadOnlyList<float[]> Vectors, IReadOnlyList<string> Contents)
        GetIdsVectorsAndContents(Guid storyId)
    {
        var response = Send<GetFullIndexResponse>(HttpMethod.Get, $"/v1/index/full?storyId={storyId}");
        return (
            response.Ids ?? new List<Guid>(),
            response.ChapterIds ?? new List<Guid>(),
            response.Vectors?.Select(v => v?.ToArray() ?? Array.Empty<float>()).ToList() ?? new List<float[]>(),
            response.Contents ?? new List<string>()
        );
    }

    public IReadOnlyList<(Guid ChunkId, float Score)> Search(Guid storyId, float[] queryVector, int topK)
    {
        if (queryVector == null || queryVector.Length == 0 || topK <= 0)
            return Array.Empty<(Guid, float)>();

        var response = Send<SearchResponse>(HttpMethod.Post, "/v1/index/search", new SearchRequest
        {
            StoryId = storyId,
            QueryVector = queryVector,
            TopK = topK
        });

        if (response.Results == null || response.Results.Count == 0)
            return Array.Empty<(Guid, float)>();

        return response.Results.Select(x => (x.ChunkId, x.Score)).ToList();
    }

    private void SendNoContent(HttpMethod method, string path, object body)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(method, path)
                {
                    Content = JsonContent.Create(body)
                };

                using var res = _http.Send(req);
                if (res.IsSuccessStatusCode) return;

                var content = SafeRead(res);
                _logger.LogWarning("FAISS service request failed {Method} {Path} -> {StatusCode}. Body={Body}",
                    method, path, (int)res.StatusCode, content);
                throw new InvalidOperationException($"FAISS service call failed ({(int)res.StatusCode}) on {path}: {content}");
            }
            catch (HttpRequestException ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "FAISS request retry {Attempt}/{MaxRetries} for {Method} {Path}", attempt + 1, _maxRetries, method, path);
                if (_retryDelayMs > 0) Task.Delay(_retryDelayMs).GetAwaiter().GetResult();
                continue;
            }
            catch (TaskCanceledException ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "FAISS timeout retry {Attempt}/{MaxRetries} for {Method} {Path}", attempt + 1, _maxRetries, method, path);
                if (_retryDelayMs > 0) Task.Delay(_retryDelayMs).GetAwaiter().GetResult();
                continue;
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Không kết nối được FAISS service ({_http.BaseAddress}) khi gọi {method} {path}. Hãy kiểm tra faiss-service đang chạy và BaseUrl.",
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException(
                    $"Gọi FAISS service bị timeout khi gọi {method} {path}. Hãy kiểm tra FAISS service hoặc tăng FaissService:TimeoutSeconds.",
                    ex);
            }
        }
    }

    private T Send<T>(HttpMethod method, string path)
        where T : new()
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(method, path);
                using var res = _http.Send(req);
                return ParseResponse<T>(method, path, res);
            }
            catch (HttpRequestException ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "FAISS request retry {Attempt}/{MaxRetries} for {Method} {Path}", attempt + 1, _maxRetries, method, path);
                if (_retryDelayMs > 0) Task.Delay(_retryDelayMs).GetAwaiter().GetResult();
                continue;
            }
            catch (TaskCanceledException ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "FAISS timeout retry {Attempt}/{MaxRetries} for {Method} {Path}", attempt + 1, _maxRetries, method, path);
                if (_retryDelayMs > 0) Task.Delay(_retryDelayMs).GetAwaiter().GetResult();
                continue;
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Không kết nối được FAISS service ({_http.BaseAddress}) khi gọi {method} {path}. Hãy kiểm tra faiss-service đang chạy và BaseUrl.",
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException(
                    $"Gọi FAISS service bị timeout khi gọi {method} {path}. Hãy kiểm tra FAISS service hoặc tăng FaissService:TimeoutSeconds.",
                    ex);
            }
        }
    }

    private T Send<T>(HttpMethod method, string path, object body)
        where T : new()
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(method, path)
                {
                    Content = JsonContent.Create(body)
                };
                using var res = _http.Send(req);
                return ParseResponse<T>(method, path, res);
            }
            catch (HttpRequestException ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "FAISS request retry {Attempt}/{MaxRetries} for {Method} {Path}", attempt + 1, _maxRetries, method, path);
                if (_retryDelayMs > 0) Task.Delay(_retryDelayMs).GetAwaiter().GetResult();
                continue;
            }
            catch (TaskCanceledException ex) when (attempt < _maxRetries)
            {
                _logger.LogWarning(ex, "FAISS timeout retry {Attempt}/{MaxRetries} for {Method} {Path}", attempt + 1, _maxRetries, method, path);
                if (_retryDelayMs > 0) Task.Delay(_retryDelayMs).GetAwaiter().GetResult();
                continue;
            }
            catch (HttpRequestException ex)
            {
                throw new InvalidOperationException(
                    $"Không kết nối được FAISS service ({_http.BaseAddress}) khi gọi {method} {path}. Hãy kiểm tra faiss-service đang chạy và BaseUrl.",
                    ex);
            }
            catch (TaskCanceledException ex)
            {
                throw new InvalidOperationException(
                    $"Gọi FAISS service bị timeout khi gọi {method} {path}. Hãy kiểm tra FAISS service hoặc tăng FaissService:TimeoutSeconds.",
                    ex);
            }
        }
    }

    private T ParseResponse<T>(HttpMethod method, string path, HttpResponseMessage res)
        where T : new()
    {
        if (!res.IsSuccessStatusCode)
        {
            var errorBody = SafeRead(res);
            _logger.LogWarning("FAISS service request failed {Method} {Path} -> {StatusCode}. Body={Body}",
                method, path, (int)res.StatusCode, errorBody);
            throw new InvalidOperationException($"FAISS service call failed ({(int)res.StatusCode}) on {path}: {errorBody}");
        }

        var content = SafeRead(res);
        if (string.IsNullOrWhiteSpace(content))
            return new T();

        var parsed = JsonSerializer.Deserialize<T>(content, _jsonOptions);
        return parsed ?? new T();
    }

    private static string SafeRead(HttpResponseMessage res)
    {
        try
        {
            return res.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch
        {
            return string.Empty;
        }
    }

    private sealed class StoryOnlyRequest
    {
        public Guid StoryId { get; set; }
    }

    private sealed class UpsertRequest
    {
        public Guid StoryId { get; set; }
        public List<Guid> ChunkIds { get; set; } = new();
        public List<Guid> ChapterIds { get; set; } = new();
        public List<float[]> Vectors { get; set; } = new();
        public List<string> Contents { get; set; } = new();
        public List<Guid> IndexedChapterIds { get; set; } = new();
    }

    private sealed class GetHasIndexResponse
    {
        public bool HasIndex { get; set; }
    }

    private sealed class GetChunkCountResponse
    {
        public int ChunkCount { get; set; }
    }

    private sealed class GetIndexedChaptersResponse
    {
        public List<Guid>? IndexedChapterIds { get; set; }
    }

    private sealed class GetChunkInfosRequest
    {
        public Guid StoryId { get; set; }
        public List<Guid> ChunkIds { get; set; } = new();
    }

    private sealed class ChunkInfoItem
    {
        public Guid ChunkId { get; set; }
        public Guid ChapterId { get; set; }
        public string? Content { get; set; }
    }

    private sealed class GetChunkInfosResponse
    {
        public List<ChunkInfoItem>? Items { get; set; }
    }

    private sealed class GetFullIndexResponse
    {
        public List<Guid>? Ids { get; set; }
        public List<Guid>? ChapterIds { get; set; }
        public List<float[]>? Vectors { get; set; }
        public List<string>? Contents { get; set; }
    }

    private sealed class SearchRequest
    {
        public Guid StoryId { get; set; }
        public float[] QueryVector { get; set; } = Array.Empty<float>();
        public int TopK { get; set; }
    }

    private sealed class SearchItem
    {
        public Guid ChunkId { get; set; }
        public float Score { get; set; }
    }

    private sealed class SearchResponse
    {
        public List<SearchItem>? Results { get; set; }
    }
}

