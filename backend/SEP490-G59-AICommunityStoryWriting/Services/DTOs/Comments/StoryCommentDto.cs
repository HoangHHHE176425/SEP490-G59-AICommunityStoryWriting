namespace Services.DTOs.Comments
{
    public class StoryCommentDto
    {
        public Guid Id { get; set; }
        public Guid StoryId { get; set; }
        public Guid? ParentId { get; set; }
        public Guid UserId { get; set; }
        public string? UserDisplayName { get; set; }
        public string Content { get; set; } = null!;
        public int LikesCount { get; set; }
        /// <summary>Current user đã thả cảm xúc (like) comment này chưa.</summary>
        public bool UserHasLiked { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

