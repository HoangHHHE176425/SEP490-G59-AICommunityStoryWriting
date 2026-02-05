using Microsoft.AspNetCore.Http;

namespace Services.DTOs.Stories
{
    public class CreateStoryRequestDto
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public List<Guid> CategoryIds { get; set; } = new();
        public string AgeRating { get; set; } = "ALL";
        /// <summary>ONGOING = Đang ra, COMPLETED = Hoàn thành, HIATUS = Tạm dừng</summary>
        public string StoryProgressStatus { get; set; } = "ONGOING";
        /// <summary>Dùng khi API chưa bật authentication (dev). Nếu có User thì ưu tiên claim.</summary>
        public Guid? AuthorId { get; set; }

        public IFormFile? CoverImage { get; set; }
    }
}
