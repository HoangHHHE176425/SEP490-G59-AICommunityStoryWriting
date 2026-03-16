using Services.DTOs.AI;

namespace Services.Interfaces
{
    /// <summary>Service gợi ý 3 hướng đi cho chương truyện tiếp theo bằng AI.</summary>
    public interface IAINextChapterService
    {
        /// <summary>
        /// Gợi ý 3 hướng đi khác nhau cho chương tiếp theo.
        /// Chỉ tác giả của truyện mới gọi được.
        /// </summary>
        /// <param name="request">StoryId bắt buộc. Luôn gợi ý sau chương mới nhất.</param>
        /// <param name="authorUserId">ID user hiện tại (phải là author của truyện).</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>3 gợi ý (title, summary, direction) hoặc exception nếu không đủ quyền / story không tồn tại / AI lỗi.</returns>
        Task<SuggestNextChapterResponse> SuggestNextChapterAsync(
            SuggestNextChapterRequest request,
            Guid authorUserId,
            CancellationToken cancellationToken = default);
    }
}
