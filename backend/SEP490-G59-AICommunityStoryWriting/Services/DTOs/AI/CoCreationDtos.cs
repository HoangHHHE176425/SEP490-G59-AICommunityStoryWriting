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

    /// <summary>
    /// Thứ tự chương đang soạn (0-based, trùng <c>chapters.order_index</c>).
    /// Khi có giá trị, bản <c>ai_generated_content</c> gắn đúng index này để so % khớp nội dung dán vào ô chương hiện tại (kể cả không bấm «áp dụng»).
    /// Null = giữ hành vi cũ: gán slot chương tiếp theo (max order_index + 1).
    /// </summary>
    public int? ChapterOrderIndex { get; set; }

    /// <summary>
    /// ID chương đang soạn do FE cấp trước (draft). Nếu chương đã tồn tại, hệ thống gắn vào <c>chapter_id</c>;
    /// nếu chưa tồn tại, sẽ lưu vào <c>draft_chapter_id</c> để bind khi tạo chương.
    /// </summary>
    public Guid? ChapterId { get; set; }
}

/// <summary>Response đồng sáng tác: dàn ý + nội dung cuối + trạng thái kiểm duyệt. Có thể kèm feedback khi ý tưởng tác giả mâu thuẫn với truyện.</summary>
public class CoCreationResponse
{
    /// <summary>Nếu có: ý tưởng tác giả mâu thuẫn với ngữ cảnh (vd. nhân vật đã chết nhưng ý tưởng nhắc nhân vật đó). Khi đó không tạo dàn ý/nội dung.</summary>
    public string? IdeaContradictionFeedback { get; set; }

    /// <summary>Cảnh báo xung đột ý tưởng (nếu có) nhưng hệ thống vẫn tiếp tục viết theo hướng đã điều chỉnh.</summary>
    public string? IdeaConflictWarning { get; set; }

    /// <summary>Dàn ý do Agent 1 tạo (rỗng nếu IdeaContradictionFeedback có giá trị).</summary>
    public string Outline { get; set; } = null!;

    /// <summary>Tiêu đề chương gợi ý (Agent 1). Null nếu không sinh được hoặc nhánh mâu thuẫn ý tưởng.</summary>
    public string? SuggestedChapterTitle { get; set; }

    /// <summary>Danh sách nhân vật tham gia, được trích từ dàn ý Agent 1.</summary>
    public List<string>? CharactersInvolved { get; set; }

    /// <summary>Nội dung cuối (đã qua kiểm duyệt hoặc bản cuối sau khi hết số lần sửa). Rỗng nếu IdeaContradictionFeedback có giá trị.</summary>
    public string FinalContent { get; set; } = null!;

    /// <summary>True nếu bản nháp qua từ cấm và chính tả (khi bật tự sửa), hoặc chỉ từ cấm khi tắt tự sửa.</summary>
    public bool Approved { get; set; }

    /// <summary>Số lần Agent 2 viết lại do từ cấm hoặc chính tả (tự sửa). 0 nếu tắt CoCreateEnableSelfCorrection hoặc không cần sửa.</summary>
    public int RevisionCount { get; set; }

    /// <summary>Không dùng; giữ field để tương thích client cũ.</summary>
    public List<string>? RevisionFeedbacks { get; set; }

    /// <summary>Khi Approved=false: lý do (từ cấm và/hoặc chính tả còn sót sau các lần sửa tự động).</summary>
    public string? ReviewFeedback { get; set; }

    /// <summary>ID chương nếu đã tồn tại <c>chapters</c> trùng <c>story_id</c> + <c>order_index</c> với slot co-create; nếu không có chương tại slot đó thì null.</summary>
    public Guid? ChapterId { get; set; }

    /// <summary>ID bản ghi ai_generated_content vừa lưu (chapter_id null cho đến khi tác giả tạo chương qua API chapters).</summary>
    public Guid? AiGeneratedContentId { get; set; }

    /// <summary>Thứ tự chương dự kiến (khớp <c>chapter_index</c> và sẽ khớp <c>chapters.order_index</c> khi tạo chương).</summary>
    public int? ChapterIndex { get; set; }

    /// <summary>Thời gian chạy từng bước (ms): Outline, Write, Guardrail, Length_Expand, … Null nếu không đo.</summary>
    public List<AgentDuration>? AgentDurations { get; set; }

    /// <summary>Khi có giá trị: có chương trước slot đang soạn chưa PUBLISHED nhưng đã có nội dung — bản AI có thể lệch mạch nháp.</summary>
    public string? ContextWarning { get; set; }
}

/// <summary>Một bước trong pipeline co-create và thời gian chạy (ms).</summary>
public class AgentDuration
{
    /// <summary>Tên bước: Outline, Write, Guardrail, Length_Expand, Length_Expand_Guardrail, …</summary>
    public string Step { get; set; } = null!;
    /// <summary>Thời gian chạy (millisecond).</summary>
    public long DurationMs { get; set; }
}

/// <summary>Event tiến độ gửi qua SSE khi chạy co-create stream: mỗi bước xong (Outline, Write, Guardrail, …) gửi một event.</summary>
public class CoCreateProgressEvent
{
    public string Step { get; set; } = null!;
    public long DurationMs { get; set; }
    /// <summary>Message hiển thị cho user (vd. "Đã xong dàn ý").</summary>
    public string? Message { get; set; }
}
