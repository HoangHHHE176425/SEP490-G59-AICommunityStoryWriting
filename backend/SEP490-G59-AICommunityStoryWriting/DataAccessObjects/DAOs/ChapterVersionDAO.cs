using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

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

        /// <summary>Batch: mỗi chapter_id → max(created_at) của version PENDING_REVIEW (cho SLA / mốc gửi chỉnh sửa).</summary>
        public static Dictionary<Guid, DateTime> GetMaxPendingReviewCreatedAtByChapterIds(IReadOnlyList<Guid> chapterIds)
        {
            var result = new Dictionary<Guid, DateTime>();
            if (chapterIds == null || chapterIds.Count == 0)
                return result;

            var idSet = chapterIds.ToHashSet();
            using var context = new StoryPlatformDbContext();
            var rows = context.chapter_versions
                .AsNoTracking()
                .Where(v => v.chapter_id != null && v.status == "PENDING_REVIEW" && idSet.Contains(v.chapter_id.Value))
                .Select(v => new { v.chapter_id, v.created_at })
                .ToList();

            foreach (var g in rows.Where(r => r.chapter_id.HasValue && r.created_at.HasValue).GroupBy(r => r.chapter_id!.Value))
            {
                var max = g.Max(x => x.created_at!.Value);
                result[g.Key] = max;
            }

            return result;
        }

        /// <summary>
        /// Lịch sử version đã từng bị từ chối: có rejection_reason (reviewed_at có thể null ở dữ liệu cũ).
        /// Dùng cho màn quản lý xuất bản (tab Từ chối).
        /// </summary>
        public static List<chapter_versions> GetRejectedHistory(Guid? moderatorId = null)
        {
            using var context = new StoryPlatformDbContext();
            var q = context.chapter_versions
                .AsNoTracking()
                .Include(v => v.chapter)
                .ThenInclude(c => c!.story)
                .Where(v => v.rejection_reason != null);
            // Không lọc theo moderator để tab lịch sử không bị thiếu (reviewed_by có thể null tùy dữ liệu/migration).
            return q
                .OrderByDescending(v => v.reviewed_at ?? v.created_at)
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

        /// <summary>Xóa mọi version gắn với chapter (trước khi xóa chapter — tránh lỗi FK).</summary>
        public static void DeleteAllByChapterId(Guid chapterId)
        {
            using var context = new StoryPlatformDbContext();
            var rows = context.chapter_versions.Where(v => v.chapter_id == chapterId).ToList();
            if (rows.Count == 0)
                return;
            context.chapter_versions.RemoveRange(rows);
            context.SaveChanges();
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

        /// <summary>Lấy danh sách chapter_id có ít nhất một version đang PENDING_REVIEW (để moderator nhận duyệt version của chapter đã xuất bản).</summary>
        public static List<Guid> GetChapterIdsWithPendingReviewVersion()
        {
            using var context = new StoryPlatformDbContext();
            return context.chapter_versions
                .Where(v => v.chapter_id != null && v.status == "PENDING_REVIEW")
                .Select(v => v.chapter_id!.Value)
                .Distinct()
                .ToList();
        }

        /// <summary>
        /// Lấy AI similarity mới nhất (>0) cho mỗi chapter từ bảng chapter_versions.
        /// Dùng fallback khi cột ai_similarity_percent của bảng chapters chưa được đồng bộ.
        /// </summary>
        public static Dictionary<Guid, decimal> GetLatestAiSimilarityPercentByChapterIds(IReadOnlyList<Guid> chapterIds)
        {
            var result = new Dictionary<Guid, decimal>();
            if (chapterIds == null || chapterIds.Count == 0)
                return result;

            var idSet = chapterIds.ToHashSet();
            try
            {
                using var context = new StoryPlatformDbContext();
                var rows = context.chapter_versions
                    .AsNoTracking()
                    .Where(v => v.chapter_id != null &&
                                idSet.Contains(v.chapter_id.Value) &&
                                v.ai_similarity_percent != null &&
                                v.ai_similarity_percent > 0)
                    .Select(v => new
                    {
                        ChapterId = v.chapter_id!.Value,
                        Ai = v.ai_similarity_percent!.Value,
                        CreatedAt = v.created_at,
                        ReviewedAt = v.reviewed_at
                    })
                    .ToList();

                foreach (var g in rows.GroupBy(x => x.ChapterId))
                {
                    var latest = g
                        .OrderByDescending(x => x.ReviewedAt ?? x.CreatedAt ?? DateTime.MinValue)
                        .FirstOrDefault();
                    if (latest != null && latest.Ai > 0)
                        result[g.Key] = latest.Ai;
                }
            }
            catch
            {
                // Không làm fail API chapter list nếu môi trường DB/model chưa map cột ai_similarity_percent.
                return result;
            }

            return result;
        }
    }
}
