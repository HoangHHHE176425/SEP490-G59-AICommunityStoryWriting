using BusinessObjects;
using BusinessObjects.Entities;

namespace DataAccessObjects.DAOs
{
    /// Lưu version (snapshot) của chapter khi tác giả chỉnh sửa sau khi đã public.
    public static class ChapterVersionDAO
    {
        /// Tạo bản ghi chapter_versions từ chapter hiện tại (trước khi áp dụng chỉnh sửa).
        /// Chỉ gọi khi chapter đang ở trạng thái PUBLISHED.
        /// authorId: lấy từ story.author_id của story chứa chapter.
        public static void SaveVersion(chapters chapter, Guid authorId, string? changeSummary = null)
        {
            if (chapter == null)
                return;

            using var context = new StoryPlatformDbContext();
            var nextVersion = context.chapter_versions
                .Where(v => v.chapter_id == chapter.id)
                .Select(v => v.version_number)
                .DefaultIfEmpty(0)
                .Max() + 1;

            var version = new chapter_versions
            {
                id = Guid.NewGuid(),
                chapter_id = chapter.id,
                author_id = authorId,
                content_snapshot = chapter.content,
                version_number = nextVersion,
                change_summary = changeSummary,
                created_at = DateTime.UtcNow
            };

            context.chapter_versions.Add(version);
            context.SaveChanges();
        }
    }
}
