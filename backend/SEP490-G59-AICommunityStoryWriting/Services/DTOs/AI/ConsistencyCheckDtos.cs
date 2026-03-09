namespace Services.DTOs.AI;

/// <summary>Request kiểm tra nhất quán: so sánh bản nháp chương với cốt truyện (các chương trước).</summary>
public class ConsistencyCheckRequest
{
    /// <summary>ID truyện (bắt buộc).</summary>
    public Guid StoryId { get; set; }

    /// <summary>Nội dung bản nháp chương mà tác giả vừa viết (cần kiểm tra).</summary>
    public string DraftContent { get; set; } = null!;

    /// <summary>ID chương ngay trước bản nháp (ngữ cảnh = các chương có order_index &lt;= chương này). Null = dùng tất cả chương hiện có.</summary>
    public Guid? AfterChapterId { get; set; }

    /// <summary>Tiêu đề chương (tùy chọn, giúp AI mô tả lỗi rõ hơn).</summary>
    public string? ChapterTitle { get; set; }
}

/// <summary>Một lỗi/ cảnh báo nhất quán (nhân vật, sự kiện, logic).</summary>
public class ConsistencyIssue
{
    /// <summary>Loại: character, event, timeline, location, other.</summary>
    public string Type { get; set; } = null!;

    /// <summary>Mô tả ngắn gọn cho tác giả (ví dụ: "Nhân vật A đã chết ở chương 2 nhưng xuất hiện trong nội dung mới").</summary>
    public string Description { get; set; } = null!;

    /// <summary>Chương tham chiếu (nếu có), ví dụ 2.</summary>
    public int? ReferenceChapter { get; set; }
}

/// <summary>Response kiểm tra nhất quán: có lỗi hay không và danh sách chi tiết.</summary>
public class ConsistencyCheckResponse
{
    /// <summary>Có ít nhất một lỗi/ cảnh báo nhất quán.</summary>
    public bool HasIssues { get; set; }

    /// <summary>Danh sách lỗi/ cảnh báo (rỗng nếu HasIssues = false).</summary>
    public List<ConsistencyIssue> Issues { get; set; } = new();
}
