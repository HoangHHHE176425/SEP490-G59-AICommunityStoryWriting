using Services.DTOs.Admin;

namespace Services.Interfaces;

public interface IAuthorAiTokenAutoGrantService
{
    Task<IReadOnlyList<AuthorAiTokenAutoGrantRuleDto>> ListRulesAsync(CancellationToken cancellationToken = default);

    Task<AuthorAiTokenAutoGrantRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<AuthorAiTokenAutoGrantRuleDto> CreateAsync(
        AuthorAiTokenAutoGrantRuleUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthorAiTokenAutoGrantRuleDto?> UpdateAsync(
        Guid id,
        AuthorAiTokenAutoGrantRuleUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Áp dụng mọi quy tắc bật khi sang chu kỳ UTC mới (so với <c>last_executed_period_key</c>).</summary>
    Task<int> ProcessDueRulesAsync(CancellationToken cancellationToken = default);

    /// <summary>Chạy ngay một quy tắc cho chu kỳ UTC hiện tại (cập nhật last key để job định kỳ không trùng).</summary>
    Task<AuthorAiTokenAutoGrantRunResultDto?> RunRuleNowAsync(Guid ruleId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Khi user vừa trở thành AUTHOR: thêm userId vào selected_user_ids của rule "mặc định cho tác giả mới"
    /// và cộng ngay grant_amount vào users.ai_token_limit (mỗi user chỉ cộng 1 lần).
    /// </summary>
    Task<bool> OnAuthorBecameAuthorAsync(Guid authorUserId, CancellationToken cancellationToken = default);
}
