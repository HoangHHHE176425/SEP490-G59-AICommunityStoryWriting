using Services.DTOs.Stories;

public interface IStoryService
{
    StoryResponseDto Create(CreateStoryRequestDto request, int authorId, string? coverImageUrl);
    PagedResultDto<StoryListItemDto> GetAll(StoryQueryDto query);
    StoryResponseDto? GetById(int id);
    StoryResponseDto? GetBySlug(string slug);
    PagedResultDto<StoryListItemDto> GetByAuthor(int authorId, StoryQueryDto query);
    bool Update(int id, UpdateStoryRequestDto request);
    bool Delete(int id);
    bool Publish(int id);
    bool Unpublish(int id);
}
