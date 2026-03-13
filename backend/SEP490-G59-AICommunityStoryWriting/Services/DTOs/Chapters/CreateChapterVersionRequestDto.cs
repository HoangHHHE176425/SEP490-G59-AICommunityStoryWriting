namespace Services.DTOs.Chapters
{
    public class CreateChapterVersionRequestDto
    {
        /// <summary>Tiêu đề của version (vd: "Bản chỉnh sửa lần 2").</summary>
        public string? TitleSnapshot { get; set; }
        /// <summary>Nội dung snapshot. Nếu null sẽ copy từ chapter hiện tại.</summary>
        public string? ContentSnapshot { get; set; }
    }
}
