using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>AI kiểm tra chương: lỗi chính tả, vi phạm chính sách, nội dung không phù hợp.</summary>
public interface IChapterCheckService
{
    /// <summary>Kiểm tra nội dung chương: chính tả (tiếng Việt/Anh), vi phạm chính sách nền tảng, nội dung không phù hợp (bạo lực, nhạy cảm, kích động).</summary>
    Task<CheckChapterResponse> CheckAsync(CheckChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>Chỉ kiểm tra chính tả (không chạy từ cấm). Dùng cho đồng sáng tác sau khi đã kiểm tra từ cấm riêng.</summary>
    Task<CheckChapterResponse> CheckSpellingOnlyAsync(CheckChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);
}
