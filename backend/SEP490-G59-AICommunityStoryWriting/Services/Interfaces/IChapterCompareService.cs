using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>So sánh nội dung chương tác giả với bản AI sinh ra (độ giống).</summary>
public interface IChapterCompareService
{
    /// <summary>Tính độ giống (0–100%) giữa chapters.content và ai_generated_content.ai_output mới nhất. Trả về HasBothContents = false nếu thiếu dữ liệu.</summary>
    Task<CompareChapterResponse> CompareAsync(CompareChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);
}
