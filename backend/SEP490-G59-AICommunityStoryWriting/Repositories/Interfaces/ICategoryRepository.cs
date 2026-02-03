using BusinessObjects.Entities;

namespace Repositories
{
    public interface ICategoryRepository
    {
        IQueryable<categories> GetAll();
        categories? GetById(Guid id);
        categories? GetBySlug(string slug);
        void Add(categories category);
        void Update(categories category);
        void Delete(Guid id);
    }
}
