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
            // Check if slug already exists
            var slug = GenerateSlug(request.Name);
            var existingCategory = _categoryRepository.GetBySlug(slug);
            if (existingCategory != null)
            {
                throw new InvalidOperationException($"Category with slug '{slug}' already exists.");
            }

            var category = new category
            {
                name = request.Name,
                slug = slug,
                description = request.Description,
                icon_url = request.IconUrl,
                is_active = request.IsActive,
                created_at = DateTime.Now
            };

            _categoryRepository.Add(category);

            return MapToDto(category);
        }

        public IEnumerable<CategoryResponseDto> GetAll(bool includeInactive = false)
        {
            var query = _categoryRepository.GetAll();

            if (!includeInactive)
            {
                query = query.Where(c => c.is_active == true);
            }

            var categories = query.OrderBy(c => c.name).ToList();
            return categories.Select(c => MapToDto(c));
        }

        public CategoryResponseDto? GetById(int id)
        {
            var category = _categoryRepository.GetById(id);
            return category == null ? null : MapToDto(category);
        }

        public CategoryResponseDto? GetBySlug(string slug)
        {
            var category = _categoryRepository.GetBySlug(slug);
            return category == null ? null : MapToDto(category);
        }

        public bool Update(int id, UpdateCategoryRequestDto request)
        {
            var category = _categoryRepository.GetById(id);
            if (category == null)
                return false;

            // Check if new slug conflicts with existing category
            var newSlug = GenerateSlug(request.Name);
            if (newSlug != category.slug)
            {
                var existingCategory = _categoryRepository.GetBySlug(newSlug);
                if (existingCategory != null && existingCategory.id != id)
                {
                    throw new InvalidOperationException($"Category with slug '{newSlug}' already exists.");
                }
            }

            category.name = request.Name;
            category.slug = newSlug;
            category.description = request.Description;
            category.icon_url = request.IconUrl;
            category.is_active = request.IsActive;

            _categoryRepository.Update(category);
            return true;
        }

        public bool Delete(int id)
        {
            var category = _categoryRepository.GetById(id);
            if (category == null)
                return false;

            // Check if category has stories
            var storyCount = StoryDAO.GetAll()
                .Where(s => s.category_id == id)
                .Count();

            if (storyCount > 0)
            {
                throw new InvalidOperationException("Cannot delete category that has associated stories.");
            }

            _categoryRepository.Delete(id);
            return true;
        }

        public bool ToggleActive(int id)
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

        private CategoryResponseDto MapToDto(category category)
        {
            // Count stories for this category using the repository
            // Note: This queries the database, so it's accurate even if navigation property isn't loaded
            var storyCount = StoryDAO.GetAll()
                .Where(s => s.category_id == category.id)
                .Count();

            return new CategoryResponseDto
            {
                Id = category.id,
                Name = category.name,
                Slug = category.slug,
                Description = category.description,
                IconUrl = category.icon_url,
                IsActive = category.is_active ?? true,
                CreatedAt = category.created_at,
                StoryCount = storyCount
            };
        }
    }
}
