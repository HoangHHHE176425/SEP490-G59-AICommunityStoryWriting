namespace Services.DTOs.AI;

/// <summary>Request đồng sáng tác: tác giả chỉ nhập ý tưởng; hệ thống tự lấy ngữ cảnh từ chương mới nhất.</summary>
public class CoCreationRequest
{
    /// <summary>ID truyện (bắt buộc).</summary>
    public Guid StoryId { get; set; }

    /// <summary>
    /// Ý tưởng của tác giả (tùy chọn). Nếu để trống/null, hệ thống sẽ tự viết chương tiếp theo dựa trên mạch truyện hiện có.
    /// </summary>
    public string? AuthorIdea { get; set; }
}

/// <summary>Response đồng sáng tác: dàn ý + nội dung cuối + trạng thái kiểm duyệt. Có thể kèm feedback khi ý tưởng tác giả mâu thuẫn với truyện.</summary>
public class CoCreationResponse
{
    /// <summary>Nếu có: ý tưởng tác giả mâu thuẫn với ngữ cảnh (vd. nhân vật đã chết nhưng ý tưởng nhắc nhân vật đó). Khi đó không tạo dàn ý/nội dung.</summary>
    public string? IdeaContradictionFeedback { get; set; }

    /// <summary>Dàn ý do Agent 1 tạo (rỗng nếu IdeaContradictionFeedback có giá trị).</summary>
    public string Outline { get; set; } = null!;

    /// <summary>Nội dung cuối (đã qua kiểm duyệt hoặc bản cuối sau khi hết số lần sửa). Rỗng nếu IdeaContradictionFeedback có giá trị.</summary>
    public string FinalContent { get; set; } = null!;

    /// <summary>Nội dung đã được Agent 3 duyệt đạt hay chưa.</summary>
    public bool Approved { get; set; }

    /// <summary>Số lần Agent 2 viết lại theo feedback (0 = không sửa).</summary>
    public int RevisionCount { get; set; }

    /// <summary>Feedback từ Agent 3 ở mỗi lần chưa đạt (khiến hệ thống chạy vòng sửa). revisionCount=1 → 1 phần tử (feedback lần 1); revisionCount=2 → 2 phần tử. Null hoặc rỗng khi revisionCount=0.</summary>
    public List<string>? RevisionFeedbacks { get; set; }

    /// <summary>Feedback cuối từ Agent 3 nếu vẫn chưa đạt sau tất cả vòng sửa (để tác giả tham khảo). Khi Approved=true thì thường null.</summary>
    public string? ReviewFeedback { get; set; }

    /// <summary>ID chương nháp (DRAFT) vừa tạo — mỗi lần co-create thành công tạo một chương + một bản ai_generated_content.</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>ID bản ghi ai_generated_content vừa lưu (gắn với ChapterId).</summary>
    public Guid? AiGeneratedContentId { get; set; }

    /// <summary>Thời gian chạy từng bước (ms): Outline, Write, Guardrail, Review; khi song song là thời gian wall-clock của mỗi phase. Null nếu không đo.</summary>
    public List<AgentDuration>? AgentDurations { get; set; }
}

/// <summary>Một bước trong pipeline co-create và thời gian chạy (ms).</summary>
public class AgentDuration
{
    /// <summary>Tên bước: Outline, Write, Guardrail, Review; hoặc Write_2, Review_2 khi chạy song song; Revision_Write khi trong vòng sửa.</summary>
    public string Step { get; set; } = null!;
    /// <summary>Thời gian chạy (millisecond).</summary>
    public long DurationMs { get; set; }
}

/// <summary>Event tiến độ gửi qua SSE khi chạy co-create stream: mỗi bước xong (Outline, Write, Guardrail, Review) gửi một event.</summary>
public class CoCreateProgressEvent
{
    public string Step { get; set; } = null!;
    public long DurationMs { get; set; }
    /// <summary>Message hiển thị cho user (vd. "Đã xong dàn ý").</summary>
    public string? Message { get; set; }
}
