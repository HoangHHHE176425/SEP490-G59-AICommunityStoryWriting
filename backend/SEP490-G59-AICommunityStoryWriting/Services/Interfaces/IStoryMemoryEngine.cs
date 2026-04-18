namespace Services.Interfaces;

/// <summary>Story Memory Engine: tổng hợp 4 loại memory (Story Context, Character Memory, Event Memory, Story State) thành một block context cho AI.</summary>
public interface IStoryMemoryEngine
{
    /// <summary>Build full context cho đồng sáng tác: Story Context chỉ từ RAG + Character + Event + Story State + ý tưởng. Cần gọi index-rag trước.</summary>
    /// <param name="authorIdeaForPrompt">Văn bản trong block "Ý tưởng tác giả" và hướng dẫn cho agent (có thể là câu mặc định khi không có gợi ý).</param>
    /// <param name="ragQueryForRetrieval">Chuỗi dùng để embed và tìm chunk RAG; nên khác câu meta khi không có gợi ý (vd. title/summary).</param>
    Task<string> BuildContextForCoCreateAsync(Guid storyId, string authorIdeaForPrompt, string ragQueryForRetrieval, CancellationToken cancellationToken = default);

    /// <summary>Build full context cho gợi ý chương tiếp theo: RAG (query = ragQuery, vd. summary + chương cuối) + Character + Event + Story State. Gợi ý sát logic truyện, đồng bộ với co-create.</summary>
    Task<string> BuildContextForSuggestAsync(Guid storyId, string ragQuery, CancellationToken cancellationToken = default);
}
