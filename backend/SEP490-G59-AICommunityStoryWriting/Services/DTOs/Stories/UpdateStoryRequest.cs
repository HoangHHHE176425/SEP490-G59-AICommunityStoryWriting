using Microsoft.AspNetCore.Http;

namespace Services.DTOs.Stories
{
    public class UpdateStoryRequestDto
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public List<Guid> CategoryIds { get; set; } = new();
        public string? Status { get; set; }
        public string? AgeRating { get; set; }
        /// <summary>ONGOING = Đang ra, COMPLETED = Hoàn thành, HIATUS = Tạm dừng</summary>
        public string? StoryProgressStatus { get; set; }
        public string? CoverImageUrl { get; set; }
    }

    public class UpdateStoryWithImageRequestDto
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public List<Guid> CategoryIds { get; set; } = new();
        public string? Status { get; set; }
        public string? AgeRating { get; set; }
        /// <summary>ONGOING = Đang ra, COMPLETED = Hoàn thành, HIATUS = Tạm dừng</summary>
        public string? StoryProgressStatus { get; set; }
        public IFormFile? CoverImage { get; set; }
    }
}
