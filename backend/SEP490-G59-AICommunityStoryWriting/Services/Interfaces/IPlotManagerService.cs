namespace Services.Interfaces;

/// <summary>Agent 4 – Plot Manager: từ nội dung chương mới (đã lưu), cập nhật timeline (event memory), character state, story state. Gọi sau khi lưu/xuất bản chương.</summary>
public interface IPlotManagerService
{
    /// <summary>Phân tích nội dung chương, trích xuất sự kiện + cập nhật nhân vật + story state; lưu vào DB. Có thể gọi re-index RAG sau.</summary>
    Task UpdateMemoryFromChapterAsync(Guid storyId, Guid chapterId, string chapterContent, bool reIndexRagAfter, CancellationToken cancellationToken = default);
}
