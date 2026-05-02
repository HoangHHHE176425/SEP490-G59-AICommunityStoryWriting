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
    //add vector đã embed vào file (ghi đè nếu đã có), format: [version][numIndexedChapters][indexedChapterIds...][numChunks][chunkId, chapterId, vecLen, vec..., contentLen, content...]. Sau khi ghi xong thì load vào cache luôn để lần sau đọc nhanh.
    public void AddVectors(Guid storyId, IReadOnlyList<Guid> chunkIds, IReadOnlyList<Guid> chapterIds, IReadOnlyList<float[]> vectors, IReadOnlyList<string> contents, IReadOnlyList<Guid> indexedChapterIds)
    {
        if (chunkIds.Count == 0 || chunkIds.Count != vectors.Count || chunkIds.Count != contents.Count || chunkIds.Count != chapterIds.Count)
            return;
        //lấy đường dẫn file lưu vector vào theo storyId
        var path = GetFilePath(storyId);
        //lấy thư mục cha của file
        var dir = Path.GetDirectoryName(path);
        //nếu thư mục cha không tồn tại thì tạo mới
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);
        //mở/tạo file để ghi đè
        using (var fs = File.Create(path))
        //tạo BinaryWriter để ghi dữ liệu vào file theo format đã định, tự động đóng stream sau khi xong
        using (var bw = new BinaryWriter(fs, Encoding.UTF8, leaveOpen: false))
        {
            //ghi version để sau này nếu format thay đổi thì có thể đọc được hoặc bỏ qua file cũ
            bw.Write(FileFormatVersion);
            //ghi số lượng chapter đã index và danh sách chapterId đã index (để lần sau chỉ cần index thêm chương mới)
            bw.Write(indexedChapterIds.Count);
            //duyệt từng chapterId đã index và ghi vào file dưới dạng byte[16] (format của Guid)
            foreach (var id in indexedChapterIds)
                bw.Write(id.ToByteArray());
            //ghi số lượng chunk mới được thêm vào (có thể là tất cả nếu index lần đầu, hoặc chỉ chunk của chương mới nếu index tăng dần)
            bw.Write(chunkIds.Count);
            //duyệt từng chunk mới và ghi chunkId, chapterId, vector, content vào file theo format đã định
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
        //sau khi ghi xong thì load toàn bộ file vào cache để lần sau đọc nhanh, tránh phải đọc file nhiều lần nếu có nhiều truy vấn liên tiếp
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
    //tìm các chunk liên quan đến queryVector bằng cách tính cosine similarity giữa queryVector và vector của từng chunk trong cache, sau đó sắp xếp theo điểm số và trả về topK chunkId cùng điểm số. Nếu cache trống hoặc queryVector rỗng thì trả về danh sách rỗng.
    public IReadOnlyList<(Guid ChunkId, float Score)> Search(Guid storyId, float[] queryVector, int topK)
    {
        //lấy toàn bộ dữ liệu đã cache cho truyện(danh sách idChunk,danh sách vector tương ứng), nếu chưa có thì load từ file vào cache.
        var c = GetOrLoadFull(storyId);
        //nếu kh có dữ liệu thì trả danh sách rỗng
        if (c.Ids.Length == 0 || queryVector.Length == 0)
            return Array.Empty<(Guid, float)>();
        //tạo list tạm để chứa cặp chunkId và điểm
        var scored = new List<(Guid id, float score)>();
        //duyệt từng chunk trong truyện, tính cosine similarity giữa vector của chunk và queryVector, lưu vào list tạm dưới dạng (chunkId, score)
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
    //Tính độ giống nhau cosine giữa hai vector a và b. Nếu một trong hai vector rỗng hoặc có độ dài khác nhau thì trả về 0. Công thức: cosine_similarity = dot(a,b) / (||a|| * ||b||), trong đó dot(a,b) là tích vô hướng của a và b, ||a|| là chuẩn L2 của a. Kết quả nằm trong khoảng [-1, 1], giá trị càng gần 1 nghĩa là hai vector càng giống nhau.
    private static float CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length == 0 || b.Length != a.Length)
            return 0f;
        //dot : tổng tích từng cặp phần tử
        //normA:tổng bình phương các phần tử của a 
        //normB: tổng bình phương các phần tử của a 
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
