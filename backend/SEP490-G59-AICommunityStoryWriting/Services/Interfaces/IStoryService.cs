using Services.DTOs.Stories;

public interface IStoryService
{
    StoryResponseDto Create(CreateStoryRequestDto request, Guid authorId, string? coverImageUrl);
    PagedResultDto<StoryListItemDto> GetAll(StoryQueryDto query);
    StoryResponseDto? GetById(Guid id);
    StoryResponseDto? GetBySlug(string slug);
    PagedResultDto<StoryListItemDto> GetByAuthor(Guid authorId, StoryQueryDto query);
    bool Update(Guid id, UpdateStoryRequestDto request);
    bool Delete(Guid id);
    bool Publish(Guid id);
    bool Unpublish(Guid id);
    /// <summary>Ghi nhận 1 lượt xem nếu viewer chưa xem story trong thời gian chống spam (cooldown). Tránh spam mở liên tục.</summary>
    void RecordViewIfAllowed(Guid storyId, string viewerKey);
    /// <summary>Ghi nhận user đã đọc story (dùng để chặn rating khi chưa đọc).</summary>
    void RecordReadStory(Guid storyId, Guid userId, string? ipAddress = null, string? deviceInfo = null);
    /// <summary>Ghi nhận user đã đọc chapter của story (dùng để chặn rating nếu chưa đọc chapter).</summary>
    void RecordReadChapter(Guid storyId, Guid chapterId, Guid userId, string? ipAddress = null, string? deviceInfo = null);
    /// <summary>Đánh giá story (1..5 sao). Chặn nếu user chưa đọc story.</summary>
    (decimal avgRating, int ratingCount) RateStory(Guid storyId, Guid userId, int starValue, string? reviewText);
}