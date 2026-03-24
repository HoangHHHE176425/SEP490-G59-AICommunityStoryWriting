namespace Services.DTOs.Community
{
    /// <summary>Thống kê công khai cho trang chủ — cùng tập truyện với danh sách công khai (guest).</summary>
    public class CommunityStatsDto
    {
        public int PublishedStoriesCount { get; set; }
        public int AuthorsCount { get; set; }
        public long TotalViews { get; set; }
    }
}
