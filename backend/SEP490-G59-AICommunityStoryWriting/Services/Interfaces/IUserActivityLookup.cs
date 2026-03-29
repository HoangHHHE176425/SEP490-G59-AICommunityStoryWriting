namespace Services.Interfaces;

/// <summary>Tra cứu hoạt động user (đọc truyện/chương) — tách khỏi DAO static để unit test.</summary>
public interface IUserActivityLookup
{
    bool HasReadAnyChapterOfStory(Guid userId, Guid storyId);
}
