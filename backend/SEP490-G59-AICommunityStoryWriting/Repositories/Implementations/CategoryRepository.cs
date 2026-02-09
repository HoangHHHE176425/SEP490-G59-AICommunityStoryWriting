using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        public IQueryable<categories> GetAll()
            => CategoryDAO.GetAll();

        public categories? GetById(Guid id)
            => CategoryDAO.GetById(id);

        public categories? GetBySlug(string slug)
            => CategoryDAO.GetBySlug(slug);

        public void Add(categories category)
            => CategoryDAO.Add(category);

        public void Update(categories category)
            => CategoryDAO.Update(category);

        public void Delete(Guid id)
            => CategoryDAO.Delete(id);
    }
}