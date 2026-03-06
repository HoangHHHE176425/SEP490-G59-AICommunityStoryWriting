using Services.DTOs.Categories;

namespace Services.Interfaces
{
    public interface ICategoryService
    {
        CategoryResponseDto Create(CreateCategoryRequestDto request);
        IEnumerable<CategoryResponseDto> GetAll(bool includeInactive = false, Guid? parentId = null, bool rootsOnly = false);
        /// <summary>Lấy danh sách categories với pagination và filtering</summary>
        PagedResultDto<CategoryResponseDto> GetAll(CategoryQueryDto query);
        CategoryResponseDto? GetById(Guid id);
        CategoryResponseDto? GetBySlug(string slug);
        bool Update(Guid id, UpdateCategoryRequestDto request);
        bool Delete(Guid id);
        bool ToggleActive(Guid id);
    }
}