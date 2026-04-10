namespace Services.DTOs.Admin.Compliance;

public class ComplianceLogItemDto
{
    public string Source { get; set; } = "";
    public Guid RowId { get; set; }
    public Guid ComplianceUserId { get; set; }
    public string? ComplianceUserName { get; set; }
    public string? TargetType { get; set; }
    public Guid? TargetId { get; set; }
    public string? Status { get; set; }
    public string? Action { get; set; }
    public string? Message { get; set; }
    /// <summary>Nhãn mô tả đối tượng xử lý (vd: truyện "A", bình luận #xyz, tài khoản ...).</summary>
    public string? TargetLabel { get; set; }
    /// <summary>Nhãn chủ nhân đối tượng (vd: tác giả/người bình luận + tên hiển thị).</summary>
    public string? OwnerLabel { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
}
