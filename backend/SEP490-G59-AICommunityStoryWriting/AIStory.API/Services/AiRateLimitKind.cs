namespace AIStory.API.Services;

/// <summary>Loại API có giới hạn số lần/24h (rolling) tách biệt.</summary>
public enum AiRateLimitKind
{
    SuggestNextChapter = 0,
    CoCreate = 1
}
