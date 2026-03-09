namespace Services.DTOs.AI;

/// <summary>Request đồng sáng tác: ý tưởng tác giả → dàn ý → nội dung → kiểm duyệt.</summary>
public class CoCreationRequest
{
    /// <summary>ID truyện (bắt buộc).</summary>
    public Guid StoryId { get; set; }

    /// <summary>Ý tưởng của tác giả (1–2 câu hoặc đoạn ngắn mô tả hướng nội dung muốn viết).</summary>
    public string AuthorIdea { get; set; } = null!;

    /// <summary>ID chương sau đó lấy ngữ cảnh (tùy chọn). Null = dùng đến chương mới nhất.</summary>
    public Guid? AfterChapterId { get; set; }

    /// <summary>Điểm cần nhất quán: trạng thái nhân vật, sự kiện quan trọng từ các chương xa (tùy chọn). Luôn được đưa vào ngữ cảnh để tránh mâu thuẫn dù nhân vật không xuất hiện trong N chương gần nhất.</summary>
    public string? ContinuityNotes { get; set; }
}

/// <summary>Response đồng sáng tác: dàn ý + nội dung cuối + trạng thái kiểm duyệt.</summary>
public class CoCreationResponse
{
    /// <summary>Dàn ý do Agent 1 tạo.</summary>
    public string Outline { get; set; } = null!;

    /// <summary>Nội dung cuối (đã qua kiểm duyệt hoặc bản cuối sau khi hết số lần sửa).</summary>
    public string FinalContent { get; set; } = null!;

    /// <summary>Nội dung đã được Agent 3 duyệt đạt hay chưa.</summary>
    public bool Approved { get; set; }

    /// <summary>Số lần Agent 2 viết lại theo feedback (0 = không sửa).</summary>
    public int RevisionCount { get; set; }

    /// <summary>Feedback cuối từ Agent 3 nếu vẫn chưa đạt (để tác giả tham khảo).</summary>
    public string? ReviewFeedback { get; set; }
}
