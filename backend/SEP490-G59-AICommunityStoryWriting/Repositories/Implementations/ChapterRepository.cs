using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories
{
    public class ChapterRepository : IChapterRepository
    {
        public IQueryable<chapters> GetAll()
            => ChapterDAO.GetAll();

        public chapters? GetById(Guid id)
            => ChapterDAO.GetById(id);

        public IEnumerable<chapters> GetByStoryId(Guid storyId)
            => ChapterDAO.GetAll()
                .Where(c => c.story_id == storyId)
                .ToList();

        public IReadOnlyList<chapters> GetPublishedByStoryId(Guid storyId)
            => ChapterDAO.GetAll()
                // EF Core chỉ translate được biểu thức dạng "c.status == 'PUBLISHED'" .
             
                .Where(c =>
                    c.story_id == storyId
                    && c.status != null
                    && c.status.ToUpper() == "PUBLISHED")
                .OrderBy(c => c.order_index)
                .ToList();

        public chapters? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex)
            => ChapterDAO.GetByStoryIdAndOrderIndex(storyId, orderIndex);

        public void Add(chapters chapter)
            => ChapterDAO.Add(chapter);

        public void Update(chapters chapter)
            => ChapterDAO.Update(chapter);

        public void Delete(Guid id)
            => ChapterDAO.Delete(id);

        public void DeleteByStoryId(Guid storyId)
            => ChapterDAO.DeleteByStoryId(storyId);
    }
}