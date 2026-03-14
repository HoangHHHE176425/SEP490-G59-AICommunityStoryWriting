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
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được tạo version cho chapter.");

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
                throw new UnauthorizedAccessException("Chỉ tác giả mới được sửa version.");
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
                throw new UnauthorizedAccessException("Chỉ tác giả mới được xóa version.");
            if (v.status == "PENDING_REVIEW")
                throw new InvalidOperationException("Không thể xóa version đang chờ duyệt.");
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
                throw new UnauthorizedAccessException("Chỉ tác giả mới được gửi duyệt version.");
            if (v.status != "DRAFT" && v.status != null)
                throw new InvalidOperationException("Chỉ version DRAFT mới được gửi duyệt.");
            if (string.IsNullOrWhiteSpace(v.content_snapshot))
                throw new InvalidOperationException("Version chưa có nội dung.");

            var chapter = _chapterRepository.GetById(v.chapter_id.Value);
            if (chapter == null) return false;
            var story = StoryDAO.GetById(chapter.story_id ?? Guid.Empty);
            if (story == null || story.author_id != authorId)
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được gửi duyệt.");

            ChapterVersionDAO.SetPendingVersionsToDraft(v.chapter_id.Value, exceptVersionId: versionId);
            v.status = "PENDING_REVIEW";
            _versionRepository.Update(v);

            // Chỉ chuyển chapter sang chờ duyệt. Không ghi đè title/content lên chapter cho đến khi moderator duyệt (approve).
            chapter.updated_at = DateTime.UtcNow;
            chapter.status = "PENDING_REVIEW";
            _chapterRepository.Update(chapter);
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
                CreatedAt = v.created_at
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
