using BusinessObjects.Entities;

namespace Repositories
{
    public interface IChapterVersionRepository
    {
        IEnumerable<chapter_versions> GetByChapterId(Guid chapterId);
        chapter_versions? GetById(Guid id);
        void Add(chapter_versions version);
        void Update(chapter_versions version);
        void Delete(Guid id);
        void DeleteAllByChapterId(Guid chapterId);
    }
}
