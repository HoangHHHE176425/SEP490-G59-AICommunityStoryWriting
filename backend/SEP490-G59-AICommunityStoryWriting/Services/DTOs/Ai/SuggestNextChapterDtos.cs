namespace Services.DTOs.Ai;

/// <summary>Một gợi ý cho chương tiếp theo.</summary>
public class NextChapterSuggestionItemDto
{
    public string Title { get; set; } = null!;
    public string Description { get; set; } = null!;
}

/// <summary>Response từ API gợi ý chương tiếp theo.</summary>
public class SuggestNextChapterResponseDto
{
    public Guid StoryId { get; set; }
    public string StoryTitle { get; set; } = null!;
    public int NextChapterNumber { get; set; }
    public IReadOnlyList<NextChapterSuggestionItemDto> Suggestions { get; set; } = null!;
    public string? ModelUsed { get; set; }
    public int? PromptTokens { get; set; }
    public int? CompletionTokens { get; set; }
    /// <summary>True khi dùng gợi ý mẫu (chế độ demo hoặc fallback do hết quota).</summary>
    public bool IsFallbackOrDemo { get; set; }
}
