using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Vector store tương thích FAISS: lưu vector ra file (một file mỗi story), tìm kiếm cosine trong memory. Có thể thay bằng FAISS native sau.</summary>
public class FaissVectorStore : IVectorStore
{
    private readonly string _basePath;
    private static readonly string FileExtension = ".faiss.bin";
    private readonly ConcurrentDictionary<Guid, (Guid[] Ids, float[][] Vectors)> _cache = new();

    public FaissVectorStore(IConfiguration configuration)
    {
        _basePath = configuration["VectorStore:Path"] ?? "Data/faiss";
        var dir = Path.GetFullPath(_basePath);
        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);
    }

    private string GetFilePath(Guid storyId) => Path.Combine(_basePath, storyId.ToString("N") + FileExtension);

    public bool HasIndex(Guid storyId)
    {
        if (_cache.TryGetValue(storyId, out _))
            return true;
        return File.Exists(GetFilePath(storyId));
    }

    public void DeleteStory(Guid storyId)
    {
        _cache.TryRemove(storyId, out _);
        var path = GetFilePath(storyId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void AddVectors(Guid storyId, IReadOnlyList<Guid> chunkIds, IReadOnlyList<float[]> vectors)
    {
        if (chunkIds.Count != vectors.Count || chunkIds.Count == 0)
            return;
        var path = GetFilePath(storyId);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (var fs = File.Create(path))
        using (var bw = new BinaryWriter(fs))
        {
            bw.Write(chunkIds.Count);
            for (int i = 0; i < chunkIds.Count; i++)
            {
                var id = chunkIds[i];
                var vec = vectors[i];
                bw.Write(id.ToByteArray());
                bw.Write(vec.Length);
                for (int j = 0; j < vec.Length; j++)
                    bw.Write(vec[j]);
            }
        }

        LoadIntoCache(storyId);
    }

    public IReadOnlyList<(Guid ChunkId, float Score)> Search(Guid storyId, float[] queryVector, int topK)
    {
        var (ids, vectors) = GetOrLoad(storyId);
        if (ids.Length == 0 || queryVector.Length == 0)
            return Array.Empty<(Guid, float)>();

        var scored = new List<(Guid id, float score)>();
        for (int i = 0; i < vectors.Length; i++)
        {
            var score = CosineSimilarity(vectors[i], queryVector);
            scored.Add((ids[i], score));
        }
        return scored.OrderByDescending(x => x.score).Take(topK).ToList();
    }

    private (Guid[] Ids, float[][] Vectors) GetOrLoad(Guid storyId)
    {
        if (_cache.TryGetValue(storyId, out var c))
            return c;
        return LoadIntoCache(storyId);
    }

    private (Guid[] Ids, float[][] Vectors) LoadIntoCache(Guid storyId)
    {
        var path = GetFilePath(storyId);
        if (!File.Exists(path))
        {
            _cache.TryAdd(storyId, (Array.Empty<Guid>(), Array.Empty<float[]>()));
            return (Array.Empty<Guid>(), Array.Empty<float[]>());
        }
        using var fs = File.OpenRead(path);
        using var br = new BinaryReader(fs);
        var count = br.ReadInt32();
        var ids = new Guid[count];
        var vectors = new float[count][];
        for (int i = 0; i < count; i++)
        {
            ids[i] = new Guid(br.ReadBytes(16));
            var len = br.ReadInt32();
            vectors[i] = new float[len];
            for (int j = 0; j < len; j++)
                vectors[i][j] = br.ReadSingle();
        }
        var pair = (ids, vectors);
        _cache.AddOrUpdate(storyId, pair, (_, _) => pair);
        return pair;
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
