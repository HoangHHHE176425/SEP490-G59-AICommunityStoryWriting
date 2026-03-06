using BusinessObjects;
using BusinessObjects.Entities;

namespace DataAccessObjects.DAOs
{
    /// Lưu version (snapshot) của story khi tác giả chỉnh sửa sau khi đã public.
    public static class StoryVersionDAO
    {
        /// Tạo bản ghi story_versions từ story hiện tại (trước khi áp dụng chỉnh sửa).
        /// Chỉ gọi khi story đang ở trạng thái PUBLISHED.
        public static void SaveVersion(stories story, string? changeSummary = null)
        {
            if (story == null || !story.author_id.HasValue)
                return;

            using var context = new StoryPlatformDbContext();
            var nextVersion = context.story_versions
                .Where(v => v.story_id == story.id)
                .Select(v => v.version_number)
                .DefaultIfEmpty(0)
                .Max() + 1;

            var version = new story_versions
            {
                id = Guid.NewGuid(),
                story_id = story.id,
                author_id = story.author_id.Value,
                title_snapshot = story.title,
                summary_snapshot = story.summary,
                cover_image_snapshot = story.cover_image,
                status_snapshot = story.status,
                version_number = nextVersion,
                change_summary = changeSummary,
                created_at = DateTime.UtcNow
            };

            context.story_versions.Add(version);
            context.SaveChanges();
        }
    }
}
