using Services.DTOs.Chapters;
using Services.DTOs.Moderation;
using Services.DTOs.Stories;

namespace Services.Interfaces
{
    public interface IModerationService
    {
        /// <param name="moderatorId">Id moderator đang xem queue; null = ADMIN (không loại trừ story bị lock bởi người khác).</param>
        /// <param name="categoryIdsFilter">Null = tất cả (ADMIN). Non-null = chỉ truyện có ít nhất 1 category trùng với category moderator được gán.</param>
        /// <param name="claimFilter">all = tất cả; unclaimed = chỉ chưa ai nhận; claimed = chỉ đã nhận duyệt (của tôi, hoặc tất cả nếu ADMIN).</param>
        PagedResultDto<StoryListItemDto> GetPendingStories(int page = 1, int pageSize = 20, string? search = null, string? sortBy = null, string? sortOrder = null, IReadOnlyList<Guid>? categoryIdsFilter = null, Guid? moderatorId = null, string? claimFilter = null);

        /// <param name="moderatorId">Id moderator đang xem queue; null = ADMIN (không loại trừ chapter bị lock bởi người khác).</param>
        /// <param name="categoryIdsFilter">Null = tất cả (ADMIN). Non-null = chỉ chapter thuộc truyện có ít nhất 1 category trùng.</param>
        /// <param name="claimFilter">all = tất cả; unclaimed = chỉ chưa ai nhận; claimed = chỉ đã nhận duyệt (của tôi, hoặc tất cả nếu ADMIN).</param>
        PagedResultDto<ChapterListItemDto> GetPendingChapters(int page = 1, int pageSize = 20, Guid? storyId = null, string? search = null, string? sortBy = null, string? sortOrder = null, IReadOnlyList<Guid>? categoryIdsFilter = null, Guid? moderatorId = null, string? claimFilter = null);

        /// <summary>Lịch sử chương đã duyệt/từ chối: status = PUBLISHED hoặc REJECTED. Khi reviewedByModeratorChapterIds set = chỉ lấy chương do moderator đó duyệt (từ moderator_logs).</summary>
        PagedResultDto<ChapterListItemDto> GetReviewedChapters(int page = 1, int pageSize = 20, string status = "REJECTED", string? search = null, string? sortBy = null, string? sortOrder = null, IReadOnlyList<Guid>? categoryIdsFilter = null, IReadOnlyList<Guid>? reviewedByModeratorChapterIds = null);

        /// <summary>Moderator "nhận duyệt" truyện → lock, người khác không thấy trong queue. Trả về true nếu claim thành công.</summary>
        bool ClaimStory(Guid storyId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        /// <summary>Moderator "nhận duyệt" chapter → lock. Trả về true nếu claim thành công.</summary>
        bool ClaimChapter(Guid chapterId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        /// <param name="allowedCategoryIds">Null = ADMIN (cho phép mọi truyện). Non-null = moderator chỉ được duyệt truyện thuộc ít nhất 1 category được gán.</param>
        bool ApproveStory(Guid storyId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        bool RejectStory(Guid storyId, Guid moderatorId, string reason, IReadOnlyList<Guid>? allowedCategoryIds = null);

        bool ApproveChapter(Guid chapterId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        bool RejectChapter(Guid chapterId, Guid moderatorId, string reason, IReadOnlyList<Guid>? allowedCategoryIds = null);
    }
}
