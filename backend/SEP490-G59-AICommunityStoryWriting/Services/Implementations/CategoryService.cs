using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories;
using Services.DTOs.Categories;
using Services.Interfaces;

namespace Services.Implementations
{
    public class CategoryService : ICategoryService
    {
        private readonly ICategoryRepository _categoryRepository;

        public CategoryService(ICategoryRepository categoryRepository)
        {
            _categoryRepository = categoryRepository;
        }

        public CategoryResponseDto Create(CreateCategoryRequestDto request)
        {
            categories? parent = null;
            if (request.ParentId.HasValue)
            {
                parent = _categoryRepository.GetById(request.ParentId.Value);
                if (parent == null)
                    throw new InvalidOperationException($"Parent category with ID {request.ParentId} not found.");
            }

            var slug = BuildUniqueSlug(request.Name, request.ParentId, parent?.slug, idToExclude: null);

            var category = new categories
            {
                id = Guid.NewGuid(),
                name = request.Name,
                slug = slug,
                description = request.Description,
                icon_url = request.IconUrl,
                is_active = request.IsActive,
                parent_category_id = request.ParentId,
                created_at = DateTime.Now
            };

            _categoryRepository.Add(category);

            return MapToDto(category);
        }

        public IEnumerable<CategoryResponseDto> GetAll(bool includeInactive = false, Guid? parentId = null, bool rootsOnly = false)
        {
            var query = _categoryRepository.GetAll();

            if (!includeInactive)
            {
                query = query.Where(c => c.is_active == true);
            }

            if (rootsOnly)
                query = query.Where(c => c.parent_category_id == null);
            else if (parentId.HasValue)
                query = query.Where(c => c.parent_category_id == parentId.Value);

            var categories = query.OrderBy(c => c.parent_category_id == null ? 0 : 1).ThenBy(c => c.name).ToList();
            return categories.Select(c => MapToDto(c));
        }

        public CategoryResponseDto? GetById(Guid id)
        {
            var category = _categoryRepository.GetById(id);
            return category == null ? null : MapToDto(category);
        }

        public CategoryResponseDto? GetBySlug(string slug)
        {
            var category = _categoryRepository.GetBySlug(slug);
            return category == null ? null : MapToDto(category);
        }

        public bool Update(Guid id, UpdateCategoryRequestDto request)
        {
            var category = _categoryRepository.GetById(id);
            if (category == null)
                return false;

            if (request.ParentId.HasValue)
            {
                if (request.ParentId.Value == id)
                    throw new InvalidOperationException("Category cannot be its own parent.");
                var parent = _categoryRepository.GetById(request.ParentId.Value);
                if (parent == null)
                    throw new InvalidOperationException($"Parent category with ID {request.ParentId} not found.");
            }

            categories? parentCat = request.ParentId.HasValue ? _categoryRepository.GetById(request.ParentId.Value) : null;
            var newSlug = BuildUniqueSlug(request.Name, request.ParentId, parentCat?.slug, idToExclude: id);

            category.name = request.Name;
            category.slug = newSlug;
            category.description = request.Description;
            category.icon_url = request.IconUrl;
            category.is_active = request.IsActive;
            category.parent_category_id = request.ParentId;

            _categoryRepository.Update(category);
            return true;
        }

        public bool Delete(Guid id)
        {
            var category = _categoryRepository.GetById(id);
            if (category == null)
                return false;

            var hasChildren = _categoryRepository.GetAll().Any(c => c.parent_category_id == id);
            if (hasChildren)
                throw new InvalidOperationException("Cannot delete category that has child categories. Delete or move children first.");

            var storyCount = CategoryDAO.GetStoryCountByCategoryId(id);
            if (storyCount > 0)
            {
                throw new InvalidOperationException("Cannot delete category that has associated stories.");
            }

            _categoryRepository.Delete(id);
            return true;
        }

        public bool ToggleActive(Guid id)
        {
            var category = _categoryRepository.GetById(id);
            if (category == null)
                return false;

            category.is_active = !category.is_active;
            _categoryRepository.Update(category);
            return true;
        }

        private string GenerateSlug(string name)
        {
            return name
                .ToLower()
                .Trim()
                .Replace(" ", "-")
                .Replace("đ", "d")
                .Replace("Đ", "d")
                .Replace("á", "a")
                .Replace("à", "a")
                .Replace("ả", "a")
                .Replace("ã", "a")
                .Replace("ạ", "a")
                .Replace("ă", "a")
                .Replace("ắ", "a")
                .Replace("ằ", "a")
                .Replace("ẳ", "a")
                .Replace("ẵ", "a")
                .Replace("ặ", "a")
                .Replace("â", "a")
                .Replace("ấ", "a")
                .Replace("ầ", "a")
                .Replace("ẩ", "a")
                .Replace("ẫ", "a")
                .Replace("ậ", "a")
                .Replace("é", "e")
                .Replace("è", "e")
                .Replace("ẻ", "e")
                .Replace("ẽ", "e")
                .Replace("ẹ", "e")
                .Replace("ê", "e")
                .Replace("ế", "e")
                .Replace("ề", "e")
                .Replace("ể", "e")
                .Replace("ễ", "e")
                .Replace("ệ", "e")
                .Replace("í", "i")
                .Replace("ì", "i")
                .Replace("ỉ", "i")
                .Replace("ĩ", "i")
                .Replace("ị", "i")
                .Replace("ó", "o")
                .Replace("ò", "o")
                .Replace("ỏ", "o")
                .Replace("õ", "o")
                .Replace("ọ", "o")
                .Replace("ô", "o")
                .Replace("ố", "o")
                .Replace("ồ", "o")
                .Replace("ổ", "o")
                .Replace("ỗ", "o")
                .Replace("ộ", "o")
                .Replace("ơ", "o")
                .Replace("ớ", "o")
                .Replace("ờ", "o")
                .Replace("ở", "o")
                .Replace("ỡ", "o")
                .Replace("ợ", "o")
                .Replace("ú", "u")
                .Replace("ù", "u")
                .Replace("ủ", "u")
                .Replace("ũ", "u")
                .Replace("ụ", "u")
                .Replace("ư", "u")
                .Replace("ứ", "u")
                .Replace("ừ", "u")
                .Replace("ử", "u")
                .Replace("ữ", "u")
                .Replace("ự", "u")
                .Replace("ý", "y")
                .Replace("ỳ", "y")
                .Replace("ỷ", "y")
                .Replace("ỹ", "y")
                .Replace("ỵ", "y");
        }

        private string BuildUniqueSlug(string name, Guid? parentId, string? parentSlug, Guid? idToExclude)
        {
            var baseSlug = string.IsNullOrEmpty(parentSlug)
                ? GenerateSlug(name)
                : parentSlug + "-" + GenerateSlug(name);
            var slug = baseSlug;
            var suffix = 0;
            while (true)
            {
                var existing = _categoryRepository.GetBySlug(slug);
                if (existing == null || (idToExclude.HasValue && existing.id == idToExclude.Value))
                    return slug;
                suffix++;
                slug = baseSlug + "-" + suffix;
            }
        }

        private CategoryResponseDto MapToDto(categories category)
        {
            var storyCount = CategoryDAO.GetStoryCountByCategoryId(category.id);

            categories? parent = category.parent_category_id.HasValue
                ? _categoryRepository.GetById(category.parent_category_id.Value)
                : null;

            return new CategoryResponseDto
            {
                Id = category.id,
                Name = category.name,
                Slug = category.slug,
                Description = category.description,
                IconUrl = category.icon_url,
                IsActive = category.is_active ?? true,
                CreatedAt = category.created_at,
                StoryCount = storyCount,
                ParentId = category.parent_category_id,
                ParentName = parent?.name
            };
        }
    }
}
