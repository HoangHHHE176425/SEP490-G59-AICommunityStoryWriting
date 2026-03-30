namespace Services.DTOs.Authors
{
    public class AuthorFollowerListItemDto
    {
        public Guid UserId { get; set; }
        public string DisplayName { get; set; } = "";
        public string Email { get; set; } = "";
        public string? AvatarUrl { get; set; }
        public DateTime? FollowedAt { get; set; }
    }
}
