namespace Services.DTOs.Categories
{
    public class UpdateCategoryRequestDto
    {
        public string Name { get; set; } = null!;
        public string? Description { get; set; }
        public string? IconUrl { get; set; }
        public bool IsActive { get; set; } = true;
    }
}
