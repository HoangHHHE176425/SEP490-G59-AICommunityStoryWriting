using BusinessObjects.Entities;

namespace Repositories
{
    public interface IStoryRepository
    {
        IQueryable<story> GetAll();
        story? GetById(int id);
        void Add(story story);
        void Update(story story);
        void Delete(int id);

    }
}
