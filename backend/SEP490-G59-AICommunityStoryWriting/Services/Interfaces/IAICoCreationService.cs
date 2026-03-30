using Services.DTOs.AI;

namespace Services.Interfaces
{
    /// <summary>Đồng sáng tác: Dàn ý (JSON) → Viết → Guardrail (từ cấm) → Kiểm duyệt (JSON + violations) + vòng sửa. Constitutional rules trong prompt.</summary>
    public interface IAICoCreationService
    {
        /// <summary>
        /// Pipeline: Agent 1 (dàn ý) → Agent 2 (viết) → lặp kiểm tra từ cấm + chính tả và Agent 2 sửa bản nháp (nếu bật) → mở rộng độ dài nếu cần → lặp kiểm tra/sửa tương tự.
        /// </summary>
        /// <param name="request">StoryId và AuthorIdea (ý tưởng, tùy chọn). Nếu AuthorIdea trống/null thì AI tự viết theo mạch truyện hiện có.</param>
        /// <param name="authorUserId">ID user (phải là tác giả truyện).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <param name="progress">Tùy chọn: báo tiến độ từng bước (dùng cho SSE stream).</param>
        /// <returns>Dàn ý, nội dung cuối, trạng thái kiểm duyệt và số lần sửa.</returns>
        Task<CoCreationResponse> CoCreateAsync(
            CoCreationRequest request,
            Guid authorUserId,
            CancellationToken cancellationToken = default,
            IProgress<CoCreateProgressEvent>? progress = null);
    }
}
