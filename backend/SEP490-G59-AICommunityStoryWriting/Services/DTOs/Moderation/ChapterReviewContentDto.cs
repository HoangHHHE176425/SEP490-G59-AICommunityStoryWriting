namespace Services.DTOs.Moderation;

/// <summary>Nội dung chapter cho moderator duyệt: bản gốc (đã xuất bản) và bản version gửi chỉnh sửa (nếu có).</summary>
public class ChapterReviewContentDto
{
    public Guid ChapterId { get; set; }
    public string? ChapterStatus { get; set; }
    public string? OriginalTitle { get; set; }
    public string? OriginalContent { get; set; }
    /// <summary>True khi chapter đã PUBLISHED và có ít nhất một version PENDING_REVIEW (chỉnh sửa sau báo cáo vi phạm).</summary>
    public bool HasPendingVersion { get; set; }
    public List<PendingVersionItemDto> PendingVersions { get; set; } = new();
}

public class PendingVersionItemDto
{
    public Guid Id { get; set; }
    public int VersionNumber { get; set; }
    public string? TitleSnapshot { get; set; }
    public string? ContentSnapshot { get; set; }
    public string? Status { get; set; }
}
