namespace Services.DTOs.CommentReports;

/// <summary>Bật/tắt tạm khóa quyền viết; mặc định áp cho chủ comment, có thể chỉ định TargetUserId.</summary>
public class SetComplianceCommentAuthorWritingSuspendedRequestDto
{
    public bool Value { get; set; }
    public Guid? TargetUserId { get; set; }
}
