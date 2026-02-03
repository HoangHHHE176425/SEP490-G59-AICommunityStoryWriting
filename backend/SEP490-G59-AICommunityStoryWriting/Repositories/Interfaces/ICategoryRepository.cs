using BusinessObjects.Entities;

namespace Repositories
{
    public interface ICategoryRepository
    {
        IQueryable<category> GetAll();
        category? GetById(int id);
        category? GetBySlug(string slug);
        void Add(category category);
        void Update(category category);
        void Delete(int id);
    }
}
