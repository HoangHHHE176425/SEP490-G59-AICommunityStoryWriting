using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>RAG cho truyện: chunk + embedding + retrieve theo query. Dùng để lấy ngữ cảnh liên quan (nhân vật, sự kiện) thay vì cắt đều mọi chương.</summary>
public interface IStoryRagService
{
    /// <summary>Kiểm tra RAG đã bật (có cấu hình embedding) và truyện đã được index (có chunks có embedding).</summary>
    bool IsRagAvailableForStory(Guid storyId);

    /// <summary>Lấy trạng thái RAG của truyện: available, chunkCount, hasVectorIndex, embeddingConfigured.</summary>
    RagStatusResponse GetRagStatus(Guid storyId);

    /// <summary>Đảm bảo truyện đã được index: chunk các chương (theo mốc chapter nếu có), gọi embedding, lưu vào DB. Nếu chưa cấu hình embedding thì không làm gì.</summary>
    Task EnsureIndexedAsync(Guid storyId, Guid? upToChapterId, CancellationToken cancellationToken = default);

    /// <summary>Index nếu chưa có chunks (và có cấu hình embedding). Dùng trước khi retrieve để đảm bảo đã index.</summary>
    Task TryEnsureIndexedAsync(Guid storyId, Guid? upToChapterId, CancellationToken cancellationToken = default);

    /// <summary>Tìm các đoạn liên quan nhất với query (ý tưởng tác giả / nhân vật / sự kiện). Trả về text đã ghép để đưa vào prompt. Nếu RAG không dùng được thì trả về null.</summary>
    Task<string?> RetrieveContextAsync(Guid storyId, string query, int maxChars = 12000, int topK = 20, CancellationToken cancellationToken = default);
}
