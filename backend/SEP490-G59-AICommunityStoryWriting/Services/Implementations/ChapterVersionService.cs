using System;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories;
using Services.DTOs.Chapters;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ChapterVersionService : IChapterVersionService
    {
        private readonly IChapterVersionRepository _versionRepository;
        private readonly IChapterRepository _chapterRepository;

        public ChapterVersionService(IChapterVersionRepository versionRepository, IChapterRepository chapterRepository)
        {
            _versionRepository = versionRepository;
            _chapterRepository = chapterRepository;
        }

        public IReadOnlyList<ChapterVersionListItemDto> GetByChapterId(Guid chapterId)
        {
            var list = _versionRepository.GetByChapterId(chapterId);
            return list.Select(MapToListItemDto).ToList();
        }

        public ChapterVersionDetailDto? GetById(Guid id)
        {
            var v = _versionRepository.GetById(id);
            return v == null ? null : MapToDetailDto(v);
        }

        public ChapterVersionDetailDto? Create(Guid chapterId, Guid authorId, CreateChapterVersionRequestDto request)
        {
            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null) return null;
            var story = StoryDAO.GetById(chapter.story_id ?? Guid.Empty);
            if (story == null || story.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được tạo phiên bản cho chương.");
            EnsureStoryProgressAllowsChapterWrite(story, "tạo phiên bản chương");

            var nextNum = ChapterVersionDAO.GetNextVersionNumber(chapterId);
            var content = request.ContentSnapshot ?? chapter.content;
            var version = new chapter_versions
            {
                id = Guid.NewGuid(),
                chapter_id = chapterId,
                author_id = authorId,
                title_snapshot = string.IsNullOrWhiteSpace(request.TitleSnapshot) ? chapter.title : request.TitleSnapshot,
                content_snapshot = content,
                version_number = nextNum,
                status = "DRAFT",
                created_at = DateTime.UtcNow
            };
            _versionRepository.Add(version);
            return MapToDetailDto(version);
        }

        public bool Update(Guid id, Guid authorId, UpdateChapterVersionRequestDto request)
        {
            var v = _versionRepository.GetById(id);
            if (v == null) return false;
            if (v.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả mới được sửa phiên bản.");
            var chapter = v.chapter_id.HasValue ? _chapterRepository.GetById(v.chapter_id.Value) : null;
            var story = StoryDAO.GetById(chapter?.story_id ?? Guid.Empty);
            EnsureStoryProgressAllowsChapterWrite(story, "chỉnh sửa phiên bản chương");
            if (v.status != "DRAFT" && v.status != null)
                throw new InvalidOperationException("Chỉ được sửa version ở trạng thái DRAFT.");

            if (request.TitleSnapshot != null)
                v.title_snapshot = request.TitleSnapshot.Trim();
            if (request.ContentSnapshot != null)
                v.content_snapshot = request.ContentSnapshot;
            _versionRepository.Update(v);
            return true;
        }

        public bool Delete(Guid id, Guid authorId)
        {
            var v = _versionRepository.GetById(id);
            if (v == null) return false;
            if (v.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả mới được xóa phiên bản.");
            var chapter = v.chapter_id.HasValue ? _chapterRepository.GetById(v.chapter_id.Value) : null;
            var story = StoryDAO.GetById(chapter?.story_id ?? Guid.Empty);
            EnsureStoryProgressAllowsChapterWrite(story, "xóa phiên bản chương");
            if (v.status == "PENDING_REVIEW")
                throw new InvalidOperationException("Không thể xóa phiên bản đang chờ duyệt.");
            if (v.status == "PUBLISHED")
                throw new InvalidOperationException("Không thể xóa version đã xuất bản.");
            _versionRepository.Delete(id);
            return true;
        }

        public bool SubmitForReview(Guid versionId, Guid authorId)
        {
            var v = _versionRepository.GetById(versionId);
            if (v == null || !v.chapter_id.HasValue) return false;
            if (v.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả mới được gửi duyệt phiên bản.");
            var statusUpper = (v.status ?? "").Trim().ToUpperInvariant();
            if (statusUpper != "DRAFT" && statusUpper != "REJECTED")
                throw new InvalidOperationException("Chỉ version Bản nháp hoặc Bị từ chối mới được gửi duyệt.");
            if (string.IsNullOrWhiteSpace(v.content_snapshot))
                throw new InvalidOperationException("Phiên bản chưa có nội dung.");

            var chapter = _chapterRepository.GetById(v.chapter_id.Value);
            if (chapter == null) return false;
            var story = StoryDAO.GetById(chapter.story_id ?? Guid.Empty);
            if (story == null || story.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được gửi duyệt.");
            if (story.author_id is Guid storyAuthorId && UserDAO.IsAuthorWritingSuspended(storyAuthorId))
                throw new InvalidOperationException("Tác giả đang bị tạm khóa chức năng viết truyện/chương (compliance/admin), không thể gửi xuất bản.");
            EnsureStoryProgressAllowsChapterWrite(story, "gửi xuất bản phiên bản chương");

            var chapterStatusUpper = (chapter.status ?? "").Trim().ToUpperInvariant();
            if (chapterStatusUpper == "PENDING_REVIEW")
                throw new InvalidOperationException("Chương đã được gửi đi duyệt. Chỉ được gửi một bản: bản gốc chương hoặc một phiên bản.");
            if (chapterStatusUpper == "PUBLISHED")
                throw new InvalidOperationException("Chương đã xuất bản không còn được gửi duyệt phiên bản chỉnh sửa.");

            // Chỉ cho phép một version chờ duyệt tại một thời điểm. Nếu đã có version khác đang PENDING_REVIEW thì không cho gửi thêm.
            var anyOtherPending = _versionRepository.GetByChapterId(v.chapter_id.Value)
                .Any(x => string.Equals(x.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase) && x.id != versionId);
            if (anyOtherPending)
                throw new InvalidOperationException("Chỉ được gửi duyệt một version tại một thời điểm. Đã có version đang chờ duyệt, hãy hủy hoặc chờ duyệt xong rồi gửi version khác.");

            v.status = "PENDING_REVIEW";
            _versionRepository.Update(v);

            chapter.updated_at = DateTime.UtcNow;
            chapter.submitted_for_review_at = DateTime.UtcNow;
            // Chapter gốc chỉ còn DRAFT/REJECTED khi gửi version (PUBLISHED đã chặn ở trên): kiểm tra thứ tự gửi.
            if (chapter.order_index > 0 && chapter.story_id.HasValue)
            {
                var previous = _chapterRepository.GetByStoryIdAndOrderIndex(chapter.story_id.Value, chapter.order_index - 1);
                if (previous == null)
                    throw new InvalidOperationException("Phải gửi xuất bản chương theo thứ tự. Chương " + chapter.order_index + " chưa được gửi hoặc chưa duyệt, không thể gửi chương " + (chapter.order_index + 1) + ".");
                var prevStatus = (previous.status ?? "").Trim().ToUpperInvariant();
                var prevHasPendingVersion = _versionRepository.GetByChapterId(previous.id).Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
                if (prevStatus != "PUBLISHED" && prevStatus != "PENDING_REVIEW" && !prevHasPendingVersion)
                    throw new InvalidOperationException("Phải gửi xuất bản chương theo thứ tự. Chương " + chapter.order_index + " chưa được gửi hoặc chưa duyệt, không thể gửi chương " + (chapter.order_index + 1) + ".");
            }
            // Không đổi chapter.status — chapter gốc vẫn là DRAFT; chỉ version chuyển sang PENDING_REVIEW.

            _chapterRepository.Update(chapter);
            return true;
        }

        public bool CancelSubmit(Guid versionId, Guid authorId)
        {
            var v = _versionRepository.GetById(versionId);
            if (v == null || !v.chapter_id.HasValue) return false;
            if (v.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả mới được hủy gửi duyệt phiên bản.");
            if (!string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chỉ phiên bản đang chờ duyệt mới được hủy xuất bản.");

            var chapter = _chapterRepository.GetById(v.chapter_id.Value);
            if (chapter == null) return false;
            var story = StoryDAO.GetById(chapter.story_id ?? Guid.Empty);
            if (story == null || story.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được hủy gửi duyệt.");

            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeChapter, v.chapter_id.Value))
                throw new InvalidOperationException("Kiểm duyệt viên đã nhận duyệt đơn này, bạn không thể hủy gửi duyệt. Vui lòng chờ kết quả duyệt.");

            EnsureCanUnpublishChapter(chapter);

            v.status = "DRAFT";
            _versionRepository.Update(v);

            chapter.updated_at = DateTime.UtcNow;
            chapter.submitted_for_review_at = null;
            // Chỉ đưa chapter về DRAFT nếu nó đang PENDING_REVIEW (do gửi version từ chapter DRAFT). Nếu chapter đang PUBLISHED thì giữ nguyên.
            if (string.Equals(chapter.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                chapter.status = "DRAFT";
            _chapterRepository.Update(chapter);

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, v.chapter_id.Value);
            return true;
        }

        /// <summary>Hủy gửi duyệt (version) cũng phải theo thứ tự ngược: chỉ được hủy chương N nếu không còn chương nào có thứ tự > N đang xuất bản hoặc chờ duyệt.</summary>
        private void EnsureCanUnpublishChapter(chapters chapter)
        {
            var storyId = chapter.story_id ?? Guid.Empty;
            if (storyId == Guid.Empty) return;
            var allChapters = _chapterRepository.GetByStoryId(storyId).OrderBy(c => c.order_index).ToList();
            var currentIndex = chapter.order_index;
            foreach (var c in allChapters)
            {
                if (c.order_index <= currentIndex) continue;
                var status = (c.status ?? "").Trim().ToUpperInvariant();
                if (status == "PUBLISHED" || status == "PENDING_REVIEW")
                    throw new InvalidOperationException("Hủy xuất bản phải theo thứ tự ngược. Phải hủy chương " + (c.order_index + 1) + " trước rồi mới hủy chương " + (currentIndex + 1) + ".");
                var hasPendingVersion = _versionRepository.GetByChapterId(c.id).Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
                if (hasPendingVersion)
                    throw new InvalidOperationException("Hủy xuất bản phải theo thứ tự ngược. Chương " + (c.order_index + 1) + " đang có phiên bản chờ duyệt, phải xử lý trước rồi mới hủy chương " + (currentIndex + 1) + ".");
            }
        }

        private static ChapterVersionListItemDto MapToListItemDto(chapter_versions v)
        {
            return new ChapterVersionListItemDto
            {
                Id = v.id,
                ChapterId = v.chapter_id ?? Guid.Empty,
                VersionNumber = v.version_number,
                TitleSnapshot = v.title_snapshot,
                Status = v.status,
                CreatedAt = v.created_at,
                RejectionReason = v.rejection_reason,
                ReviewedAt = v.reviewed_at,
                AiSimilarityPercent = v.ai_similarity_percent
            };
        }

        private static void EnsureStoryProgressAllowsChapterWrite(stories? story, string actionVi)
        {
            var progress = (story?.story_progress_status ?? "ONGOING").Trim().ToUpperInvariant();
            if (progress == "HIATUS" || progress == "COMPLETED")
                throw new InvalidOperationException($"Truyện đang ở trạng thái {(progress == "COMPLETED" ? "Hoàn thành" : "Tạm dừng")}, không thể {actionVi}.");
        }

        private static ChapterVersionDetailDto MapToDetailDto(chapter_versions v)
        {
            return new ChapterVersionDetailDto
            {
                Id = v.id,
                ChapterId = v.chapter_id ?? Guid.Empty,
                VersionNumber = v.version_number,
                TitleSnapshot = v.title_snapshot,
                Status = v.status,
                CreatedAt = v.created_at,
                ContentSnapshot = v.content_snapshot,
                AiSimilarityPercent = v.ai_similarity_percent
            };
        }

        private static int CalculateWordCount(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return 0;
            return content.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
