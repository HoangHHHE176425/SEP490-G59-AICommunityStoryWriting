namespace Services.Interfaces;

/// <summary>Story Memory Engine: tổng hợp 4 loại memory (Story Context, Character Memory, Event Memory, Story State) thành một block context cho AI.</summary>
public interface IStoryMemoryEngine
{
    /// <summary>Build full context cho đồng sáng tác: Story Context (RAG khi đã index, không thì N chương) + Character + Event + Story State + continuity + ý tưởng.</summary>
    Task<string> BuildContextForCoCreateAsync(Guid storyId, string authorIdea, string? continuityNotes, Guid? afterChapterId, CancellationToken cancellationToken = default);
}
