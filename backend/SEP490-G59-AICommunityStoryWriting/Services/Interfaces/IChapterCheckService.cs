using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>AI kiểm tra chương: từ cấm và chính tả (qua hai API riêng).</summary>
public interface IChapterCheckService
{
    /// <summary>Chỉ kiểm tra từ cấm / guardrail (không gọi AI chính tả).</summary>
    Task<CheckChapterResponse> CheckBannedWordsOnlyAsync(CheckChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);

    /// <summary>Chỉ kiểm tra chính tả (không chạy từ cấm). Dùng cho đồng sáng tác sau khi đã kiểm tra từ cấm riêng.</summary>
    Task<CheckChapterResponse> CheckSpellingOnlyAsync(CheckChapterRequest request, Guid? userId, CancellationToken cancellationToken = default);
}
