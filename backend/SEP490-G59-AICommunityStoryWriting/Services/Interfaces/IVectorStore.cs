namespace Services.Interfaces;

/// <summary>Vector store cho RAG (FAISS): lưu vector + content theo story, tìm kiếm cosine. Chỉ dùng file, không dùng bảng story_chapter_chunks.</summary>
public interface IVectorStore
{
    /// <summary>Đã có index cho truyện (có ít nhất một chunk).</summary>
    bool HasIndex(Guid storyId);

    /// <summary>Xóa toàn bộ index của truyện.</summary>
    void DeleteStory(Guid storyId);

    /// <summary>Ghi hoặc ghi đè index: chunkIds, chapterIds, vectors, contents; indexedChapterIds = danh sách chapter đã được index (để lần sau chỉ index chương mới).</summary>
    void AddVectors(Guid storyId, IReadOnlyList<Guid> chunkIds, IReadOnlyList<Guid> chapterIds, IReadOnlyList<float[]> vectors, IReadOnlyList<string> contents, IReadOnlyList<Guid> indexedChapterIds);

    /// <summary>Danh sách chapter_id đã có trong index (để index tăng dần chỉ thêm chương mới).</summary>
    IReadOnlyList<Guid> GetIndexedChapterIds(Guid storyId);

    /// <summary>Số chunk trong index.</summary>
    int GetChunkCount(Guid storyId);

    /// <summary>Lấy (chunkId, chapterId, content) cho các chunkIds (để build context sau Search).</summary>
    IReadOnlyList<(Guid ChunkId, Guid ChapterId, string Content)> GetChunkInfos(Guid storyId, IReadOnlyList<Guid> chunkIds);

    /// <summary>Lấy (ids, chapterIds, vectors, contents) để merge khi index tăng dần.</summary>
    (IReadOnlyList<Guid> Ids, IReadOnlyList<Guid> ChapterIds, IReadOnlyList<float[]> Vectors, IReadOnlyList<string> Contents) GetIdsVectorsAndContents(Guid storyId);

    /// <summary>Tìm top-k chunk gần nhất với queryVector (cosine similarity).</summary>
    IReadOnlyList<(Guid ChunkId, float Score)> Search(Guid storyId, float[] queryVector, int topK);
}
