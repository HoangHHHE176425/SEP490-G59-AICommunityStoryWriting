namespace Services.DTOs.AI;

/// <summary>Request gợi ý 3 hướng đi cho chương tiếp theo. Luôn gợi ý sau chương mới nhất (theo thứ tự).</summary>
public class SuggestNextChapterRequest
{
    /// <summary>ID truyện (bắt buộc).</summary>
    public Guid StoryId { get; set; }
}

/// <summary>Một gợi ý hướng đi cho chương tiếp theo (chi tiết).</summary>
public class NextChapterSuggestionItemDto
{
    /// <summary>Tiêu đề gợi ý cho chương.</summary>
    public string Title { get; set; } = null!;
    /// <summary>Tóm tắt 2–4 câu mô tả hướng đi.</summary>
    public string Summary { get; set; } = null!;
    /// <summary>Mô tả chi tiết: tình tiết, cảm xúc, cách nối với phần trước.</summary>
    public string Direction { get; set; } = null!;
    /// <summary>2–4 sự kiện chính sẽ xảy ra trong chương (mỗi sự kiện một dòng hoặc đánh số).</summary>
    public string? KeyEvents { get; set; }
    /// <summary>Nhân vật chính xuất hiện / liên quan.</summary>
    public string? CharactersInvolved { get; set; }
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
