using Services.DTOs.Comments;

namespace Services.Interfaces;

/// <summary>Nghiệp vụ thêm comment cấp truyện (sau trim + guardrail).</summary>
public interface IStoryCommentPostService
{
    Task<StoryCommentPostOutcome> AddAsync(
        Guid storyId,
        Guid userId,
        string contentTrimmed,
        Guid? parentId,
        CancellationToken cancellationToken = default);
}
