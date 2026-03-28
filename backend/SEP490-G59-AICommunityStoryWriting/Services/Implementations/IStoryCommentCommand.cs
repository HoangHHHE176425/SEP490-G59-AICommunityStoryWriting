using BusinessObjects.Entities;

namespace Services.Interfaces;

/// <summary>Đọc/ghi comment gắn story (cấp truyện, chapter_id null) — adapter quanh CommentDAO.</summary>
public interface IStoryCommentCommand
{
    comments? GetById(Guid commentId);

    comments AddStoryComment(Guid storyId, Guid userId, string content, Guid? parentId);
}
