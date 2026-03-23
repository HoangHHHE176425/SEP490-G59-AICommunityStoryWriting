namespace Services.DTOs.CommentReports;

public class SetCommentThreadHiddenRequestDto
{
    /// <summary>true = ẩn; false = hiện lại.</summary>
    public bool Value { get; set; } = true;

    /// <summary>Ẩn toàn bộ reply (descendants) nếu true; chỉ ẩn comment cha nếu false.</summary>
    public bool IncludeReplies { get; set; } = true;
}

