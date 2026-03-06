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
        /// <summary>Current user đã thả cảm xúc (like) comment này chưa. Giữ cho tương thích.</summary>
        public bool UserHasLiked { get; set; }
        /// <summary>Số lượng từng loại reaction: LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW.</summary>
        public IReadOnlyDictionary<string, int> ReactionCounts { get; set; } = new Dictionary<string, int>();
        /// <summary>Loại reaction mà current user đã chọn (null nếu chưa chọn).</summary>
        public string? UserReactionType { get; set; }
        public DateTime? CreatedAt { get; set; }
    }
}

