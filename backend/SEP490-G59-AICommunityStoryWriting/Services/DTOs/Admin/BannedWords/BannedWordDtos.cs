namespace Services.DTOs.Admin.BannedWords;

/// <summary>Một từ cấm (dùng cho check-chapter).</summary>
public class BannedWordItemDto
{
    public Guid Id { get; set; }
    public string Word { get; set; } = null!;
    /// <summary>Category: "BannedWord" = từ cấm check-chapter.</summary>
    public string? Category { get; set; }
    public DateTime? CreatedAt { get; set; }
}

/// <summary>Request thêm từ cấm (admin).</summary>
public class AddBannedWordRequest
{
    /// <summary>Từ hoặc cụm từ cấm (bắt buộc).</summary>
    public string Word { get; set; } = null!;
    /// <summary>Category, mặc định "BannedWord" cho check-chapter.</summary>
    public string? Category { get; set; }
}
