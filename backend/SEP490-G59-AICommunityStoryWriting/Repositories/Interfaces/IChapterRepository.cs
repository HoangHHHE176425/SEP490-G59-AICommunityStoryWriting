using BusinessObjects.Entities;

namespace Repositories
{
    public interface IChapterRepository
    {
        IQueryable<chapters> GetAll();
        chapters? GetById(Guid id);
        IEnumerable<chapters> GetByStoryId(Guid storyId);
        chapters? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex);
        void Add(chapters chapter);
        void Update(chapters chapter);
        void Delete(Guid id);
        void DeleteByStoryId(Guid storyId);
    }
}
