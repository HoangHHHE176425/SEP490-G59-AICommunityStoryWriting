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
            // EF Core không dịch được DefaultIfEmpty(0).Max() với Max non-nullable,
            // nên dùng Max trên int? rồi fallback 0 ở client.
            var max = context.chapter_versions
                .Where(v => v.chapter_id == chapter.id)
                .Select(v => (int?)v.version_number)
                .Max() ?? 0;
            var nextVersion = max + 1;

            var version = new chapter_versions
            {
                id = Guid.NewGuid(),
                chapter_id = chapter.id,
                author_id = authorId,
                title_snapshot = chapter.title,
                content_snapshot = chapter.content,
                version_number = nextVersion,
                change_summary = changeSummary,
                status = "DRAFT",
                created_at = DateTime.UtcNow
            };

            context.chapter_versions.Add(version);
            context.SaveChanges();
        }

        public static List<chapter_versions> GetByChapterId(Guid chapterId)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapter_versions
                .Where(v => v.chapter_id == chapterId)
                .OrderBy(v => v.version_number)
                .ToList();
        }

        public static chapter_versions? GetById(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapter_versions.FirstOrDefault(v => v.id == id);
        }

        public static void Add(chapter_versions version)
        {
            using var context = new StoryPlatformDbContext();
            context.chapter_versions.Add(version);
            context.SaveChanges();
        }

        public static void Update(chapter_versions version)
        {
            using var context = new StoryPlatformDbContext();
            context.chapter_versions.Update(version);
            context.SaveChanges();
        }

        public static void Delete(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            var v = context.chapter_versions.FirstOrDefault(x => x.id == id);
            if (v != null)
            {
                context.chapter_versions.Remove(v);
                context.SaveChanges();
            }
        }

        /// <summary>Lấy số version tiếp theo cho chapter (max version_number + 1).</summary>
        public static int GetNextVersionNumber(Guid chapterId)
        {
            using var context = new StoryPlatformDbContext();
            var max = context.chapter_versions
                .Where(v => v.chapter_id == chapterId)
                .Select(v => (int?)v.version_number)
                .Max() ?? 0;
            return max + 1;
        }

        /// <summary>Đặt tất cả version có status PENDING_REVIEW của chapter về DRAFT (trừ versionId nếu chỉ định).</summary>
        public static void SetPendingVersionsToDraft(Guid chapterId, Guid? exceptVersionId = null)
        {
            using var context = new StoryPlatformDbContext();
            var list = context.chapter_versions
                .Where(v => v.chapter_id == chapterId && v.status == "PENDING_REVIEW")
                .ToList();
            foreach (var v in list)
            {
                if (exceptVersionId.HasValue && v.id == exceptVersionId.Value) continue;
                v.status = "DRAFT";
            }
            if (list.Any())
                context.SaveChanges();
        }

        /// <summary>Lấy version đang chờ duyệt (PENDING_REVIEW) của chapter, nếu có.</summary>
        public static chapter_versions? GetPendingByChapterId(Guid chapterId)
        {
            using var context = new StoryPlatformDbContext();
            return context.chapter_versions
                .FirstOrDefault(v => v.chapter_id == chapterId && v.status == "PENDING_REVIEW");
        }

        /// <summary>Khi moderator duyệt chapter: đánh dấu version đang PENDING_REVIEW của chapter đó thành PUBLISHED.</summary>
        public static void MarkPendingVersionsAsPublished(Guid chapterId)
        {
            using var context = new StoryPlatformDbContext();
            var list = context.chapter_versions
                .Where(v => v.chapter_id == chapterId && v.status == "PENDING_REVIEW")
                .ToList();
            foreach (var v in list)
                v.status = "PUBLISHED";
            if (list.Any())
                context.SaveChanges();
        }
    }
}
