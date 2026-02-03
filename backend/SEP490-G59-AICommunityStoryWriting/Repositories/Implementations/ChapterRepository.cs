using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories
{
    public class ChapterRepository : IChapterRepository
    {
        public IQueryable<chapter> GetAll()
            => ChapterDAO.GetAll();

        public chapter? GetById(int id)
            => ChapterDAO.GetById(id);

        public IEnumerable<chapter> GetByStoryId(int storyId)
            => ChapterDAO.GetAll()
                .Where(c => c.story_id == storyId)
                .ToList();

        public chapter? GetByStoryIdAndOrderIndex(int storyId, int orderIndex)
            => ChapterDAO.GetByStoryIdAndOrderIndex(storyId, orderIndex);

        public void Add(chapter chapter)
            => ChapterDAO.Add(chapter);

        public void Update(chapter chapter)
            => ChapterDAO.Update(chapter);

        public void Delete(int id)
            => ChapterDAO.Delete(id);
    }
}
