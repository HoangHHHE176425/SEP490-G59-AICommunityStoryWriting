using Microsoft.AspNetCore.Http;

namespace Services.DTOs.Stories
{
    public class CreateStoryRequestDto
    {
        public string Title { get; set; } = null!;
        public string? Summary { get; set; }
        public List<Guid> CategoryIds { get; set; } = new();
        public string AgeRating { get; set; } = "ALL";
        /// <summary>Bắt buộc khi tạo: ONGOING (Đang ra), COMPLETED (Hoàn thành), HIATUS (Tạm dừng). Không mặc định — thiếu/null → từ chối ở service.</summary>
        public string? StoryProgressStatus { get; set; }
        /// <summary>Dùng khi API chưa bật authentication (dev). Nếu có User thì ưu tiên claim.</summary>
        public Guid? AuthorId { get; set; }

        public IFormFile? CoverImage { get; set; }
    }
}