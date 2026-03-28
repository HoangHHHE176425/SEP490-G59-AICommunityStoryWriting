namespace Services.DTOs.Comments;

/// <summary>Kết quả thêm comment cấp truyện (sau khi đã validate input/guardrail ở API).</summary>
public enum StoryCommentPostStatus
{
    StoryNotFound,
    Rejected,
    Success
}

public sealed class StoryCommentPostOutcome
{
    public StoryCommentPostStatus Status { get; private init; }
    public string? Message { get; private init; }
    public StoryCommentDto? Dto { get; private init; }

    public static StoryCommentPostOutcome NotFound(string message) =>
        new() { Status = StoryCommentPostStatus.StoryNotFound, Message = message };

    public static StoryCommentPostOutcome BadRequest(string message) =>
        new() { Status = StoryCommentPostStatus.Rejected, Message = message };

    public static StoryCommentPostOutcome Ok(StoryCommentDto dto) =>
        new() { Status = StoryCommentPostStatus.Success, Dto = dto };
}
