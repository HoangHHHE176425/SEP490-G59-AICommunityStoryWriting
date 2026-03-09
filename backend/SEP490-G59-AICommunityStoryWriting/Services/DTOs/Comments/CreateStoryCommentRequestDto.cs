namespace Services.DTOs.Comments
{
    public class CreateStoryCommentRequestDto
    {
        public string Content { get; set; } = null!;
        public Guid? ParentId { get; set; }
    }
}

