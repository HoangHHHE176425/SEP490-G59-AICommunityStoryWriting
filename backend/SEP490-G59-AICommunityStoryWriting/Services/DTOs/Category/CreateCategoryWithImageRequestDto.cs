using Microsoft.AspNetCore.Http;

namespace Services.DTOs.Categories
{
    public class CreateCategoryWithImageRequestDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; } = true;
        public IFormFile? IconImage { get; set; }
    }
}
