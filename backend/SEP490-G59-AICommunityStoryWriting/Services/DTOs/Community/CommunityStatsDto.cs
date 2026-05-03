namespace Services.DTOs.Community
{
    /// <summary>Thống kê công khai cho trang chủ — truyện/views theo tập PUBLISHED + không compliance_hidden; authorsCount theo role AUTHOR trừ BANNED.</summary>
    public class CommunityStatsDto
    {
        public int PublishedStoriesCount { get; set; }
        public int AuthorsCount { get; set; }
        public long TotalViews { get; set; }
    }
}
