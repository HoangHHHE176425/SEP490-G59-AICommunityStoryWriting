namespace Services.Interfaces;

/// <summary>Context builder: Story memory (N chương gần nhất).</summary>
public interface IStoryContextBuilder
{
    /// <summary>Chỉ block truyện + story memory (N chương), không có continuity/ý tưởng. Dùng cho Story Memory Engine ghép với Character/Event/State.</summary>
    string GetStoryAndMemoryBlock(Guid storyId, Guid? afterChapterId);

    /// <summary>Build context cho gợi ý chương tiếp theo: story info + story memory (N chương gần nhất).</summary>
    string BuildForSuggestNextChapter(Guid storyId, Guid? afterChapterId);

    /// <summary>Build context cho kiểm tra nhất quán: story info + story memory (N chương gần nhất) + draft.</summary>
    string BuildForCheckConsistency(Guid storyId, string draftContent, Guid? afterChapterId, string? chapterTitle);
}
