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

            var chapterStatusUpper = (chapter.status ?? "").Trim().ToUpperInvariant();
            if (chapterStatusUpper == "PENDING_REVIEW")
                throw new InvalidOperationException("Chương đã được gửi đi duyệt. Chỉ được gửi một bản: bản gốc chương hoặc một phiên bản.");

            // Chỉ cho phép một version chờ duyệt tại một thời điểm. Nếu đã có version khác đang PENDING_REVIEW thì không cho gửi thêm.
            var anyOtherPending = _versionRepository.GetByChapterId(v.chapter_id.Value)
                .Any(x => string.Equals(x.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase) && x.id != versionId);
            if (anyOtherPending)
                throw new InvalidOperationException("Chỉ được gửi duyệt một version tại một thời điểm. Đã có version đang chờ duyệt, hãy hủy hoặc chờ duyệt xong rồi gửi version khác.");

            v.status = "PENDING_REVIEW";
            _versionRepository.Update(v);

            chapter.updated_at = DateTime.UtcNow;
            if (chapterStatusUpper == "PUBLISHED")
            {
                // Chapter đã xuất bản: giữ nguyên trạng thái PUBLISHED, chỉ version chuyển sang PENDING_REVIEW. Không ghi đè nội dung chapter để moderator thấy bản gốc và so sánh với version.
            }
            else
            {
                // Chapter đang DRAFT: phải gửi theo thứ tự — chương trước phải đã gửi (PUBLISHED hoặc PENDING_REVIEW) thì mới gửi được chương này.
                if (chapter.order_index > 0 && chapter.story_id.HasValue)
                {
                    var previous = _chapterRepository.GetByStoryIdAndOrderIndex(chapter.story_id.Value, chapter.order_index - 1);
                    var prevStatus = (previous?.status ?? "").Trim().ToUpperInvariant();
                    if (previous == null || (prevStatus != "PUBLISHED" && prevStatus != "PENDING_REVIEW"))
                        throw new InvalidOperationException("Phải gửi xuất bản chương theo thứ tự. Chương " + chapter.order_index + " chưa được gửi hoặc chưa duyệt, không thể gửi chương " + (chapter.order_index + 1) + ".");
                }
                // Chuyển chapter sang PENDING_REVIEW và đồng bộ title + content từ phiên bản để moderator xem nội dung gửi duyệt.
                chapter.status = "PENDING_REVIEW";
                if (!string.IsNullOrWhiteSpace(v.title_snapshot))
                    chapter.title = v.title_snapshot;
                chapter.content = v.content_snapshot;
                chapter.word_count = CalculateWordCount(v.content_snapshot);
            }

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

            v.status = "DRAFT";
            _versionRepository.Update(v);

            chapter.updated_at = DateTime.UtcNow;
            // Chỉ đưa chapter về DRAFT nếu nó đang PENDING_REVIEW (do gửi version từ chapter DRAFT). Nếu chapter đang PUBLISHED thì giữ nguyên.
            if (string.Equals(chapter.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                chapter.status = "DRAFT";
            _chapterRepository.Update(chapter);

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, v.chapter_id.Value);
            return true;
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
                ReviewedAt = v.reviewed_at
            };
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
                ContentSnapshot = v.content_snapshot
            };
        }

        private static int CalculateWordCount(string? content)
        {
            if (string.IsNullOrWhiteSpace(content)) return 0;
            return content.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
        }
    }
}
