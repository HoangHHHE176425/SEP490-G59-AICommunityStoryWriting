using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Services.Interfaces;

namespace Services.Implementations.Lookups
{
    public class CategoryLookup : ICategoryLookup
    {
        public categories? GetById(Guid categoryId) => CategoryDAO.GetById(categoryId);
    }
}

