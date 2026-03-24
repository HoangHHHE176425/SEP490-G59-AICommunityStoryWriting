namespace Services.DTOs.AI;

/// <summary>Request so sánh: truyền <see cref="ChapterId"/> và nội dung hiện tại để so với các bản AI theo chính chapter đó.</summary>
public class CompareChapterRequest
{
    /// <summary>ID chương (<c>chapters.id</c>).</summary>
    public Guid ChapterId { get; set; }
    /// <summary>Nội dung cần so sánh (thường là nội dung đang soạn).</summary>
    public string? Content { get; set; }
}

/// <summary>Kết quả so sánh: độ giống (0–100) và nhận định.</summary>
public class CompareChapterResponse
{
    /// <summary>Độ giống nhau 0–100 (%).</summary>
    public double SimilarityScore { get; set; }

    /// <summary>True nếu coi là giống (vd. &gt; 85%).</summary>
    public bool IsSimilar { get; set; }

    /// <summary>Độ dài nội dung tác giả (số ký tự).</summary>
    public int AuthorContentLength { get; set; }

    /// <summary>Độ dài nội dung AI (số ký tự).</summary>
    public int AiContentLength { get; set; }

    /// <summary>Có đủ dữ liệu để so sánh (có cả content tác giả và AI).</summary>
    public bool HasBothContents { get; set; }

    /// <summary>Thông báo ngắn (lý do không so sánh được hoặc tóm tắt).</summary>
    public string? Message { get; set; }
}
