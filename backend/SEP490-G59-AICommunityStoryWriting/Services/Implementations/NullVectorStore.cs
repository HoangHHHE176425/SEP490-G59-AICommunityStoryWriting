using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Vector store no-op khi không dùng FAISS (lưu vector trong SQL).</summary>
public class NullVectorStore : IVectorStore
{
    public bool HasIndex(Guid storyId) => false;
    public void DeleteStory(Guid storyId) { }
    public void AddVectors(Guid storyId, IReadOnlyList<Guid> chunkIds, IReadOnlyList<float[]> vectors) { }
    public IReadOnlyList<(Guid ChunkId, float Score)> Search(Guid storyId, float[] queryVector, int topK) => Array.Empty<(Guid, float)>();
}
