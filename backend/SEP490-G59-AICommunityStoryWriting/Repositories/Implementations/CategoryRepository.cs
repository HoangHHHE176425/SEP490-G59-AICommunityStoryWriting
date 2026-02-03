using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories
{
    public class CategoryRepository : ICategoryRepository
    {
        public IQueryable<category> GetAll()
            => CategoryDAO.GetAll();

        public category? GetById(int id)
            => CategoryDAO.GetById(id);

        public category? GetBySlug(string slug)
            => CategoryDAO.GetBySlug(slug);

        public void Add(category category)
            => CategoryDAO.Add(category);

        public void Update(category category)
            => CategoryDAO.Update(category);

        public void Delete(int id)
            => CategoryDAO.Delete(id);
    }
}
