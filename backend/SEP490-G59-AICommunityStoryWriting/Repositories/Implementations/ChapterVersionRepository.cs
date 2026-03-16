using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories
{
    public class ChapterVersionRepository : IChapterVersionRepository
    {
        public IEnumerable<chapter_versions> GetByChapterId(Guid chapterId)
            => ChapterVersionDAO.GetByChapterId(chapterId);

        public chapter_versions? GetById(Guid id)
            => ChapterVersionDAO.GetById(id);

        public void Add(chapter_versions version)
            => ChapterVersionDAO.Add(version);

        public void Update(chapter_versions version)
            => ChapterVersionDAO.Update(version);

        public void Delete(Guid id)
            => ChapterVersionDAO.Delete(id);
    }
}
