using System.Text.Json.Serialization;

namespace Services.DTOs.AI;

/// <summary>Request gợi ý 3 hướng đi cho chương tiếp theo.</summary>
public class SuggestNextChapterRequest
{
    /// <summary>ID truyện (bắt buộc).</summary>
    public Guid StoryId { get; set; }

    /// <summary>ID chương đang soạn (FE tạo trước khi lưu nội dung). Nếu có: lưu từng gợi ý vào <c>ai_generated_content</c> gắn <c>chapter_id</c> này.</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>Mốc chương cho ngữ cảnh gợi ý (up-to). Thường là chương liền trước chương đang soạn; null = sau chương cuối trong DB.</summary>
    public Guid? UpToChapterId { get; set; }

    /// <summary>Alias tương thích ngược cho client cũ đang gửi <c>afterChapterId</c>.</summary>
    [JsonPropertyName("afterChapterId")]
    public Guid? AfterChapterId
    {
        get => UpToChapterId;
        set
        {
            if (!UpToChapterId.HasValue)
                UpToChapterId = value;
        }
    }
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

    /// <summary>Khi có giá trị: có chương trước slot gợi ý chưa PUBLISHED nhưng đã có nội dung.</summary>
    public string? ContextWarning { get; set; }
}

public class SuggestNextChapterContextDto
{
    public string? StoryTitle { get; set; }
    public int ChaptersIncluded { get; set; }
}
