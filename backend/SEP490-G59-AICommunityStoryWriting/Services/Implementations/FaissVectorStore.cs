using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Configuration;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Vector store FAISS: một file mỗi story, lưu header (chapter đã index) + (chunkId, chapterId, vector, content). Chỉ dùng file, không DB.</summary>
public class FaissVectorStore : IVectorStore
{
    private const int FileFormatVersion = 2;
    private readonly string _basePath;
    private static readonly string FileExtension = ".faiss.bin";
    private readonly ConcurrentDictionary<Guid, CachedStory> _cache = new();

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
        if (_cache.TryGetValue(storyId, out var c) && c.Ids.Length > 0)
            return true;
        if (!File.Exists(GetFilePath(storyId)))
            return false;
        var count = GetChunkCount(storyId);
        return count > 0;
    }

    public void DeleteStory(Guid storyId)
    {
        _cache.TryRemove(storyId, out _);
        var path = GetFilePath(storyId);
        if (File.Exists(path))
            File.Delete(path);
    }

    public void AddVectors(Guid storyId, IReadOnlyList<Guid> chunkIds, IReadOnlyList<Guid> chapterIds, IReadOnlyList<float[]> vectors, IReadOnlyList<string> contents, IReadOnlyList<Guid> indexedChapterIds)
    {
        if (chunkIds.Count == 0 || chunkIds.Count != vectors.Count || chunkIds.Count != contents.Count || chunkIds.Count != chapterIds.Count)
            return;
        var path = GetFilePath(storyId);
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        using (var fs = File.Create(path))
        using (var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false))
        {
            bw.Write(FileFormatVersion);
            bw.Write(indexedChapterIds.Count);
            foreach (var id in indexedChapterIds)
                bw.Write(id.ToByteArray());
            bw.Write(chunkIds.Count);
            for (int i = 0; i < chunkIds.Count; i++)
            {
                bw.Write(chunkIds[i].ToByteArray());
                bw.Write(chapterIds[i].ToByteArray());
                var vec = vectors[i];
                bw.Write(vec.Length);
                for (int j = 0; j < vec.Length; j++)
                    bw.Write(vec[j]);
                var content = contents[i] ?? "";
                var contentBytes = Encoding.UTF8.GetBytes(content);
                bw.Write(contentBytes.Length);
                bw.Write(contentBytes);
            }
        }

        LoadIntoCache(storyId);
    }

    public IReadOnlyList<Guid> GetIndexedChapterIds(Guid storyId)
    {
        var path = GetFilePath(storyId);
        if (!File.Exists(path))
            return Array.Empty<Guid>();
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs, Encoding.UTF8);
            var version = br.ReadInt32();
            if (version != FileFormatVersion)
                return Array.Empty<Guid>();
            var n = br.ReadInt32();
            var list = new List<Guid>(n);
            for (int i = 0; i < n; i++)
                list.Add(new Guid(br.ReadBytes(16)));
            return list;
        }
        catch
        {
            return Array.Empty<Guid>();
        }
    }

    public int GetChunkCount(Guid storyId)
    {
        if (_cache.TryGetValue(storyId, out var c))
            return c.Ids.Length;
        var path = GetFilePath(storyId);
        if (!File.Exists(path))
            return 0;
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs, Encoding.UTF8);
            var version = br.ReadInt32();
            if (version != FileFormatVersion)
                return 0;
            var numChapters = br.ReadInt32();
            br.ReadBytes(16 * numChapters);
            return br.ReadInt32();
        }
        catch
        {
            return 0;
        }
    }
    //Lấy (chunkId, chapterId, content) cho các chunkIds (để build context sau Search). Nếu chunkId không tồn tại thì bỏ qua.
    public IReadOnlyList<(Guid ChunkId, Guid ChapterId, string Content)> GetChunkInfos(Guid storyId, IReadOnlyList<Guid> chunkIds)
    {
        if (chunkIds == null || chunkIds.Count == 0)
            return Array.Empty<(Guid, Guid, string)>();
        var c = GetOrLoadFull(storyId);
        var dict = new Dictionary<Guid, (Guid ChapterId, string Content)>();
        for (int i = 0; i < c.Ids.Length; i++)
            dict[c.Ids[i]] = (c.ChapterIds[i], c.Contents[i]);
        return chunkIds
            .Where(id => dict.ContainsKey(id))
            .Select(id => (id, dict[id].ChapterId, dict[id].Content))
            .ToList();
    }

    public (IReadOnlyList<Guid> Ids, IReadOnlyList<Guid> ChapterIds, IReadOnlyList<float[]> Vectors, IReadOnlyList<string> Contents) GetIdsVectorsAndContents(Guid storyId)
    {
        var c = GetOrLoadFull(storyId);
        return (c.Ids, c.ChapterIds, c.Vectors, c.Contents);
    }
    //Tính cosine similarity giữa queryVector và tất cả vector đã lưu, trả về top-k chunkId có điểm cao nhất. Nếu không có vector nào hoặc queryVector rỗng thì trả về rỗng.
    public IReadOnlyList<(Guid ChunkId, float Score)> Search(Guid storyId, float[] queryVector, int topK)
    {
        var c = GetOrLoadFull(storyId);
        if (c.Ids.Length == 0 || queryVector.Length == 0)
            return Array.Empty<(Guid, float)>();
        var scored = new List<(Guid id, float score)>();
        for (int i = 0; i < c.Vectors.Length; i++)
        {
            var score = CosineSimilarity(c.Vectors[i], queryVector);
            scored.Add((c.Ids[i], score));
        }
        return scored.OrderByDescending(x => x.score).Take(topK).ToList();
    }

    private CachedStory GetOrLoadFull(Guid storyId)
    {
        if (_cache.TryGetValue(storyId, out var c))
            return c;
        return LoadIntoCache(storyId);
    }
    //đọc file binary -> chuyển từ byte[] -> Guid, float[], string; lưu vào cache; nếu lỗi thì cache empty để tránh đọc lại nhiều lần.
    private CachedStory LoadIntoCache(Guid storyId)
    {
        var path = GetFilePath(storyId);
        if (!File.Exists(path))
        {
            var empty = new CachedStory(Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<float[]>(), Array.Empty<string>());
            _cache.TryAdd(storyId, empty);
            return empty;
        }
        try
        {
            using var fs = File.OpenRead(path);
            using var br = new BinaryReader(fs, Encoding.UTF8);
            var version = br.ReadInt32();
            if (version != FileFormatVersion)
            {
                var empty = new CachedStory(Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<float[]>(), Array.Empty<string>());
                _cache.TryAdd(storyId, empty);
                return empty;
            }
            var numChapters = br.ReadInt32();
            br.ReadBytes(16 * numChapters);
            var numChunks = br.ReadInt32();
            var ids = new Guid[numChunks];
            var chapterIds = new Guid[numChunks];
            var vectors = new float[numChunks][];
            var contents = new string[numChunks];
            for (int i = 0; i < numChunks; i++)
            {
                ids[i] = new Guid(br.ReadBytes(16));
                chapterIds[i] = new Guid(br.ReadBytes(16));
                var vecLen = br.ReadInt32();
                vectors[i] = new float[vecLen];
                for (int j = 0; j < vecLen; j++)
                    vectors[i][j] = br.ReadSingle();
                var contentLen = br.ReadInt32();
                contents[i] = Encoding.UTF8.GetString(br.ReadBytes(contentLen));
            }
            var cached = new CachedStory(ids, chapterIds, vectors, contents);
            _cache.AddOrUpdate(storyId, cached, (_, _) => cached);
            return cached;
        }
        catch
        {
            var empty = new CachedStory(Array.Empty<Guid>(), Array.Empty<Guid>(), Array.Empty<float[]>(), Array.Empty<string>());
            _cache.TryAdd(storyId, empty);
            return empty;
        }
    }
    //Tính tích vô hướng của 2 vector, chia cho tích của độ dài 2 vector (cosine similarity). Nếu có lỗi (độ dài 0) thì trả về 0.
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

    private sealed class CachedStory
    {
        public readonly Guid[] Ids;
        public readonly Guid[] ChapterIds;
        public readonly float[][] Vectors;
        public readonly string[] Contents;

        public CachedStory(Guid[] ids, Guid[] chapterIds, float[][] vectors, string[] contents)
        {
            Ids = ids;
            ChapterIds = chapterIds;
            Vectors = vectors;
            Contents = contents;
        }
    }
}
