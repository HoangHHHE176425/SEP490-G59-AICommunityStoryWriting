namespace Services.DTOs.AI;



/// <summary>Body <c>POST .../check-chapter-banned-words</c>: chỉ từ cấm / guardrail.</summary>

public class CheckChapterBannedWordsRequest

{

    /// <summary>Nội dung cần quét (bắt buộc).</summary>

    public string Content { get; set; } = null!;



    /// <summary>ID truyện (tùy chọn).</summary>

    public Guid? StoryId { get; set; }

}



/// <summary>Body <c>POST .../check-chapter-spelling</c>: kiểm tra chính tả (AI).</summary>

public class CheckChapterSpellingRequest

{

    /// <summary>Nội dung chương cần kiểm tra (bắt buộc).</summary>

    public string Content { get; set; } = null!;



    /// <summary>ID truyện (tùy chọn, phục vụ log usage).</summary>

    public Guid? StoryId { get; set; }



    /// <summary>Tiêu đề chương (tùy chọn; thêm ngữ cảnh cho AI và khóa cache).</summary>

    public string? ChapterTitle { get; set; }

}



/// <summary>Một lỗi chính tả: từ/cụm sai và gợi ý sửa.</summary>

public class SpellingIssue

{

    public string WordOrPhrase { get; set; } = null!;

    public string Suggestion { get; set; } = null!;

    /// <summary>Câu hoặc dòng trích từ nội dung chương, chứa từ/cụm sai (ưu tiên hiển thị thay cho tọa độ).</summary>

    public string? Context { get; set; }

}



/// <summary>Một vi phạm chính sách hoặc nội dung không phù hợp.</summary>

public class PolicyViolationItem

{

    /// <summary>Loại: PolicyViolation, InappropriateContent, BannedTopic, v.v.</summary>

    public string Type { get; set; } = null!;

    public string Description { get; set; } = null!;

    /// <summary>Đoạn trích vi phạm (tùy chọn).</summary>

    public string? Quote { get; set; }

}



/// <summary>Response kiểm tra chương: chính tả, chính sách, nội dung không phù hợp.</summary>

public class CheckChapterResponse

{

    /// <summary>True nếu không có lỗi chính tả, không vi phạm chính sách và không có nội dung không phù hợp.</summary>

    public bool Passed { get; set; }



    /// <summary>Danh sách lỗi chính tả (từ/cụm sai + gợi ý sửa).</summary>

    public List<SpellingIssue> SpellingIssues { get; set; } = new();



    /// <summary>Vi phạm chính sách hoặc nội dung không phù hợp.</summary>

    public List<PolicyViolationItem> PolicyViolations { get; set; } = new();



    /// <summary>Có nội dung không phù hợp (bạo lực, nhạy cảm, kích động, v.v.) theo chính sách nền tảng.</summary>

    public bool HasInappropriateContent { get; set; }



    /// <summary>Tóm tắt ngắn cho tác giả (tùy chọn).</summary>

    public string? Summary { get; set; }

}

