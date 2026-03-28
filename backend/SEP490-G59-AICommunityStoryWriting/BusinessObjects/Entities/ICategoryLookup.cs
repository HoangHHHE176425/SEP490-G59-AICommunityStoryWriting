using BusinessObjects.Entities;

namespace Services.Interfaces
{
    public interface ICategoryLookup
    {
        categories? GetById(Guid categoryId);
    }
}

