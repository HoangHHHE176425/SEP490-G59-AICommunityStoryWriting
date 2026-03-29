using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Services.Interfaces;

namespace Services.Implementations.Lookups;

public class StoryCommentCommand : IStoryCommentCommand
{
    public comments? GetById(Guid commentId) => CommentDAO.GetById(commentId);

    public comments AddStoryComment(Guid storyId, Guid userId, string content, Guid? parentId) =>
        CommentDAO.AddStoryComment(storyId, userId, content, parentId);
}
