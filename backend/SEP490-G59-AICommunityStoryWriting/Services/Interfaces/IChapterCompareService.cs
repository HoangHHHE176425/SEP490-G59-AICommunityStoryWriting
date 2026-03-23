using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>So sánh nội dung chương tác giả với bản AI sinh ra (độ giống).</summary>
public interface IChapterCompareService
{
    /// <summary>Tính độ giống (0–100%) theo <c>ChapterId</c> (tự resolve <c>order_index</c>). Không ghi DB — dùng khi đã có bản ghi chương.</summary>
    Task<CompareChapterResponse> CompareAsync(CompareChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>So sánh nội dung đang soạn với bản AI (trước khi tạo/cập nhật chương). Không ghi DB.</summary>
    Task<CompareChapterResponse> ComparePreviewAsync(CompareChapterPreviewRequest request, Guid? userId, CancellationToken cancellationToken = default);
}
