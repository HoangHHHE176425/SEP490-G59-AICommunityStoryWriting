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

        /// <summary>Lấy danh sách truyện đã duyệt hoặc từ chối (status = PUBLISHED | REJECTED). Non-Admin: chỉ truyện do moderator này duyệt/từ chối (theo moderator_logs). Admin có thể lọc theo moderatorIdFilter, dateFrom, dateTo.</summary>
        PagedResultDto<StoryListItemDto> GetReviewedStories(int page, int pageSize, string status, string? search, string? sortBy, string? sortOrder, IReadOnlyList<Guid>? categoryIdsFilter, Guid? moderatorId, bool isAdmin, Guid? moderatorIdFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null);

        /// <summary>Lấy danh sách chapter đã duyệt hoặc từ chối (status = PUBLISHED | REJECTED). Admin có thể lọc theo moderatorIdFilter, dateFrom, dateTo.</summary>
        PagedResultDto<ChapterListItemDto> GetReviewedChapters(int page, int pageSize, string status, string? search, string? sortBy, string? sortOrder, IReadOnlyList<Guid>? categoryIdsFilter, Guid? moderatorId, bool isAdmin, Guid? moderatorIdFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null);

        /// <summary>Moderator "nhận duyệt" truyện → lock, người khác không thấy trong queue. Trả về true nếu claim thành công.</summary>
        bool ClaimStory(Guid storyId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        /// <summary>Moderator "nhận duyệt" chapter → lock. Trả về true nếu claim thành công.</summary>
        bool ClaimChapter(Guid chapterId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        /// <param name="allowedCategoryIds">Null = ADMIN (cho phép mọi truyện). Non-null = moderator chỉ được duyệt truyện thuộc ít nhất 1 category được gán.</param>
        bool ApproveStory(Guid storyId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        bool RejectStory(Guid storyId, Guid moderatorId, string reason, IReadOnlyList<Guid>? allowedCategoryIds = null);

        bool ApproveChapter(Guid chapterId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null);

        bool RejectChapter(Guid chapterId, Guid moderatorId, string reason, IReadOnlyList<Guid>? allowedCategoryIds = null);

        /// <summary>Lấy nội dung chapter cho moderator: bản gốc (đã xuất bản) + bản version chờ duyệt (nếu có). Dùng để hiển thị 2 phiên bản khi duyệt chỉnh sửa sau báo cáo vi phạm.</summary>
        ChapterReviewContentDto? GetChapterReviewContent(Guid chapterId);
    }
}
