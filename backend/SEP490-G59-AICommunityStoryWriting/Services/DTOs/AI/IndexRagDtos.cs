namespace Services.DTOs.AI;

/// <summary>Request index RAG cho một truyện (chunk + embedding). Chỉ nội dung chương <c>PUBLISHED</c> được đưa vào index.</summary>
public class IndexRagRequest
{
    public Guid StoryId { get; set; }
    /// <summary>Chỉ index các chương <c>PUBLISHED</c> có order_index &lt;= chương mốc này. Null = mọi chương đã xuất bản.</summary>
    public Guid? AfterChapterId { get; set; }
}

/// <summary>Trạng thái RAG của một truyện (dùng cho GET /api/ai/rag-status).</summary>
public class RagStatusResponse
{
    public Guid StoryId { get; set; }
    /// <summary>RAG đã sẵn sàng cho co-create / suggest (có embedding config + có chunk + có vector index nếu dùng FAISS).</summary>
    public bool Available { get; set; }
    /// <summary>Đã cấu hình đủ để gọi API embedding (BaseUrl + model + API key với OpenAI/OpenRouter, hoặc chỉ BaseUrl với Ollama).</summary>
    public bool EmbeddingConfigured { get; set; }
    /// <summary>Số chunk trong index FAISS (chỉ dùng file, không DB).</summary>
    public int ChunkCount { get; set; }
    /// <summary>Có file/index vector (FAISS) cho truyện này. False nếu không dùng FAISS hoặc chưa index.</summary>
    public bool HasVectorIndex { get; set; }
    /// <summary>Provider vector store đang chạy (Local hoặc FaissRemote).</summary>
    public string VectorStoreProvider { get; set; } = "Local";
    /// <summary>BaseUrl FAISS service khi dùng remote (null nếu provider local).</summary>
    public string? FaissServiceBaseUrl { get; set; }
}
