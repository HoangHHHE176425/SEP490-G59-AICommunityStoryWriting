using Services.DTOs.AI;

namespace Services.Interfaces
{
    /// <summary>Đồng sáng tác: Dàn ý (JSON) → Viết → Guardrail (từ cấm) → Kiểm duyệt (JSON + violations) + vòng sửa. Constitutional rules trong prompt.</summary>
    public interface IAICoCreationService
    {
        /// <summary>
        /// Pipeline: Agent 1 (dàn ý JSON) → Agent 2 (nội dung) → Guardrail → Agent 3 (kiểm duyệt JSON). Nếu chưa đạt thì sửa theo feedback, tối đa CoCreateMaxRevisions lần.
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
