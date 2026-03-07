using Services.DTOs.AI;

namespace Services.Interfaces;

/// <summary>Kiểm tra bản nháp chương có khớp với cốt truyện (các chương trước) hay không; phát hiện mâu thuẫn nhân vật, sự kiện, logic.</summary>
public interface IAIConsistencyCheckService
{
    /// <summary>So sánh DraftContent với các chương trước (theo AfterChapterId). Trả về danh sách lỗi nhất quán nếu có.</summary>
    Task<ConsistencyCheckResponse> CheckConsistencyAsync(
        ConsistencyCheckRequest request,
        Guid authorUserId,
        CancellationToken cancellationToken = default);
}
