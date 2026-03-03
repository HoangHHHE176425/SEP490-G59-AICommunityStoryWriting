namespace Services.DTOs.AI;

/// <summary>Request gợi ý 3 hướng đi cho chương tiếp theo.</summary>
public class SuggestNextChapterRequest
{
    /// <summary>ID truyện (bắt buộc).</summary>
    public Guid StoryId { get; set; }

    /// <summary>ID chương sau đó muốn gợi ý (tùy chọn). Nếu null = gợi ý sau chương mới nhất.</summary>
    public Guid? AfterChapterId { get; set; }
}

/// <summary>Một gợi ý hướng đi cho chương tiếp theo.</summary>
public class NextChapterSuggestionItemDto
{
    public string Title { get; set; } = null!;
    public string Summary { get; set; } = null!;
    public string Direction { get; set; } = null!;
}

/// <summary>Response chứa 3 gợi ý và thông tin ngữ cảnh đã dùng.</summary>
public class SuggestNextChapterResponse
{
    public List<NextChapterSuggestionItemDto> Suggestions { get; set; } = new();
    public SuggestNextChapterContextDto? ContextUsed { get; set; }
}

public class SuggestNextChapterContextDto
{
    public string? StoryTitle { get; set; }
    public int ChaptersIncluded { get; set; }
}
