using BusinessObjects.Entities;
using Services.DTOs.Categories;

namespace Services.Interfaces
{
    public interface ICategoryService
    {
        CategoryResponseDto Create(CreateCategoryRequestDto request);
        IEnumerable<CategoryResponseDto> GetAll(bool includeInactive = false);
        CategoryResponseDto? GetById(int id);
        CategoryResponseDto? GetBySlug(string slug);
        bool Update(int id, UpdateCategoryRequestDto request);
        bool Delete(int id);
        bool ToggleActive(int id);
    }
}
