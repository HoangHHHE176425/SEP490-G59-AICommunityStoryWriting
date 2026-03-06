using BusinessObjects.Entities;

namespace Repositories
{
    public interface IStoryRepository
    {
        IQueryable<stories> GetAll();
        stories? GetById(Guid id);
        stories? GetBySlug(string slug);
        IReadOnlyList<Guid> GetStoryIdsByCategoryIds(IReadOnlyCollection<Guid> categoryIds);
        void Add(stories story);
        void Add(stories story, IEnumerable<Guid> categoryIds);
        void Update(stories story);
        void Delete(Guid id);
        void IncrementViewCount(Guid storyId);
    }
}