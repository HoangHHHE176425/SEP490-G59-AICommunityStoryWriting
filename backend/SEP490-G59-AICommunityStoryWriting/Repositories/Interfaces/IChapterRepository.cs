using BusinessObjects.Entities;

namespace Repositories
{
    public interface IChapterRepository
    {
        IQueryable<chapters> GetAll();
        chapters? GetById(Guid id);
        IEnumerable<chapters> GetByStoryId(Guid storyId);

        /// <summary>Chỉ chương <c>PUBLISHED</c>, sắp <c>order_index</c>. Dùng cho RAG / AI (ngữ cảnh “đã xuất bản”).</summary>
        IReadOnlyList<chapters> GetPublishedByStoryId(Guid storyId);

        chapters? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex);
        void Add(chapters chapter);
        void Update(chapters chapter);
        void Delete(Guid id);
        void DeleteByStoryId(Guid storyId);
    }
}