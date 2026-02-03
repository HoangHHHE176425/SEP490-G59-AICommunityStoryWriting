using Services.DTOs.Categories;

namespace Services.Interfaces
{
    public interface ICategoryService
    {
        CategoryResponseDto Create(CreateCategoryRequestDto request);
        /// <param name="parentId">Null = tất cả. Có giá trị = chỉ thể loại con của parent đó.</param>
        /// <param name="rootsOnly">True = chỉ loại truyện gốc (parent_id null).</param>
        IEnumerable<CategoryResponseDto> GetAll(bool includeInactive = false, Guid? parentId = null, bool rootsOnly = false);
        CategoryResponseDto? GetById(Guid id);
        CategoryResponseDto? GetBySlug(string slug);
        bool Update(Guid id, UpdateCategoryRequestDto request);
        bool Delete(Guid id);
        bool ToggleActive(Guid id);
    }
}
