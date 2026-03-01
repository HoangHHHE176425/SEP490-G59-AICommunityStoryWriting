using Services.DTOs.Ai;

namespace Services.Interfaces;

/// <summary>Service AI: gợi ý chương tiếp theo, đồng sáng tác (sau này).</summary>
public interface IStoryAiService
{
    /// <summary>
    /// Đọc ngữ cảnh truyện (tóm tắt + các chương đã viết) và gọi LLM để đưa ra 3–5 gợi ý cho chương tiếp theo.
    /// </summary>
    /// <param name="storyId">Id truyện</param>
    /// <param name="userId">Id user (tác giả) gọi API – dùng cho log</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Danh sách gợi ý (title + description) hoặc null nếu truyện không tồn tại / chưa cấu hình API key</returns>
    Task<SuggestNextChapterResponseDto?> SuggestNextChapterAsync(
        Guid storyId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
