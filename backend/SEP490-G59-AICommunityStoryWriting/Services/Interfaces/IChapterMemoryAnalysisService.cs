namespace Services.Interfaces;

/// <summary>Agent phân tích nội dung chương đã lưu, trích xuất và ghi vào story_character_memory, story_event_memory, story_story_state.</summary>
public interface IChapterMemoryAnalysisService
{
    /// <param name="orderIndex">Thứ tự chương trong truyện (chapters.order_index).</param>
    Task ExtractAndPersistAsync(
        Guid storyId,
        Guid chapterId,
        string chapterTitle,
        int orderIndex,
        string chapterContent,
        CancellationToken cancellationToken = default);
}
