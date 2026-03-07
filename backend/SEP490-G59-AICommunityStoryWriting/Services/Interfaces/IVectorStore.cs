namespace Services.Interfaces;

/// <summary>Vector store cho RAG (FAISS-style): lưu vector theo story, tìm kiếm tương đồng cosine.</summary>
public interface IVectorStore
{
    /// <summary>Đã có index cho truyện (có ít nhất một vector).</summary>
    bool HasIndex(Guid storyId);

    /// <summary>Xóa toàn bộ index của truyện.</summary>
    void DeleteStory(Guid storyId);

    /// <summary>Thêm vector cho các chunk (sau khi embed). Thứ tự ids tương ứng với vectors.</summary>
    void AddVectors(Guid storyId, IReadOnlyList<Guid> chunkIds, IReadOnlyList<float[]> vectors);

    /// <summary>Tìm top-k chunk gần nhất với queryVector (cosine similarity). Trả về (chunkId, score) giảm dần theo score.</summary>
    IReadOnlyList<(Guid ChunkId, float Score)> Search(Guid storyId, float[] queryVector, int topK);
}
