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
}
