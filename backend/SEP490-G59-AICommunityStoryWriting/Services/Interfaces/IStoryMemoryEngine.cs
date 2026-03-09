namespace Services.Interfaces;

/// <summary>Story Memory Engine: tổng hợp 4 loại memory (Story Context, Character Memory, Event Memory, Story State) thành một block context cho AI.</summary>
public interface IStoryMemoryEngine
{
    /// <summary>Build full context cho đồng sáng tác: Story Context chỉ từ RAG + Character + Event + Story State + ý tưởng. Cần gọi index-rag trước.</summary>
    Task<string> BuildContextForCoCreateAsync(Guid storyId, string authorIdea, CancellationToken cancellationToken = default);
}
