using Services.DTOs.AI;

namespace Services.Interfaces
{
    /// <summary>Service đồng sáng tác với 3 agent: Dàn ý → Viết nội dung → Kiểm duyệt (có vòng sửa).</summary>
    public interface IAICoCreationService
    {
        /// <summary>
        /// Chạy pipeline: ý tưởng tác giả → Agent 1 (dàn ý) → Agent 2 (nội dung) → Agent 3 (kiểm duyệt).
        /// Nếu chưa đạt, Agent 2 viết lại theo feedback, tối đa 2 lần sửa.
        /// </summary>
        /// <param name="request">StoryId, AuthorIdea bắt buộc; AfterChapterId tùy chọn.</param>
        /// <param name="authorUserId">ID user (phải là tác giả truyện).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Dàn ý, nội dung cuối, trạng thái kiểm duyệt và số lần sửa.</returns>
        Task<CoCreationResponse> CoCreateAsync(
            CoCreationRequest request,
            Guid authorUserId,
            CancellationToken cancellationToken = default);
    }
}
