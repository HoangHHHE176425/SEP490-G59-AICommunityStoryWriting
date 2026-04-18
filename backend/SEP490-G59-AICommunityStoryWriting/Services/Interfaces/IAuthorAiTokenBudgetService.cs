using Services;
using Services.DTOs.Admin;

namespace Services.Interfaces;

public interface IAuthorAiTokenBudgetService
{
    Task<AuthorAiTokenBudgetDto?> GetBudgetAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Kiểm tra mọi giới hạn đang bật (ngày/tuần/tháng/tích lũy); nếu vượt thì ném <see cref="AuthorAiTokenBudgetExceededException"/>.</summary>
    Task EnsureWithinBudgetAsync(Guid userId, CancellationToken cancellationToken = default);
}
