using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>So sánh nội dung chương tác giả với bản AI sinh ra (độ giống).</summary>
public interface IChapterCompareService
{
    /// <summary>Tính độ giống (0–100%) theo <c>ChapterId</c> và nội dung truyền vào; so với <c>ai_generated_content</c> cùng chapter.</summary>
    Task<CompareChapterResponse> CompareAsync(CompareChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);
}
