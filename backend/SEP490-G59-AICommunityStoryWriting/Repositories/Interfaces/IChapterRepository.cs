using BusinessObjects.Entities;

namespace Repositories
{
    public interface IChapterRepository
    {
        IQueryable<chapter> GetAll();
        chapter? GetById(int id);
        IEnumerable<chapter> GetByStoryId(int storyId);
        chapter? GetByStoryIdAndOrderIndex(int storyId, int orderIndex);
        void Add(chapter chapter);
        void Update(chapter chapter);
        void Delete(int id);
    }
}
