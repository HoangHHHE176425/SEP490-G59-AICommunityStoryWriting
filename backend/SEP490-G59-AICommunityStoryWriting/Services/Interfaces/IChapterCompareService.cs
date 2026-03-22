using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>So sánh nội dung chương tác giả với bản AI sinh ra (độ giống).</summary>
public interface IChapterCompareService
{
    /// <summary>Tính độ giống (0–100%) theo <c>ChapterId</c> (tự resolve <c>order_index</c>). Ghi <c>ai_similarity_percent</c> khi chương PUBLISHED.</summary>
    Task<CompareChapterResponse> CompareAsync(CompareChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);
}
