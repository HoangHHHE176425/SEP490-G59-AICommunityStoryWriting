using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Logging;
using Services.DTOs.Comments;
using Services.DTOs.Notifications;
using Services.Interfaces;

namespace Services.Implementations;

public class StoryCommentPostService : IStoryCommentPostService
{
    private readonly IStoryLookup _storyLookup;
    private readonly IUserActivityLookup _userActivityLookup;
    private readonly IStoryCommentCommand _commentCommand;
    private readonly ICommentReactionReader _reactionReader;
    private readonly INotificationHubNotifier _notificationHubNotifier;
    private readonly ILogger<StoryCommentPostService> _logger;

    public StoryCommentPostService(
        IStoryLookup storyLookup,
        IUserActivityLookup userActivityLookup,
        IStoryCommentCommand commentCommand,
        ICommentReactionReader reactionReader,
        INotificationHubNotifier notificationHubNotifier,
        ILogger<StoryCommentPostService> logger)
    {
        _storyLookup = storyLookup;
        _userActivityLookup = userActivityLookup;
        _commentCommand = commentCommand;
        _reactionReader = reactionReader;
        _notificationHubNotifier = notificationHubNotifier;
        _logger = logger;
    }

    public async Task<StoryCommentPostOutcome> AddAsync(
        Guid storyId,
        Guid userId,
        string contentTrimmed,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        var story = _storyLookup.GetById(storyId);
        if (story == null)
            return StoryCommentPostOutcome.NotFound($"Story with ID {storyId} not found");

        if (story.comments_disabled)
            return StoryCommentPostOutcome.BadRequest(
                "Truyện này đang trong quá trình xử lý vi phạm nên hiện không thể bình luận.");

        if (!string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
            return StoryCommentPostOutcome.BadRequest("Chỉ có thể comment truyện đã PUBLISHED.");

        if (!_userActivityLookup.HasReadAnyChapterOfStory(userId, storyId))
            return StoryCommentPostOutcome.BadRequest("Bạn cần đọc ít nhất một chapter trước khi comment.");

        comments? parent = null;
        if (parentId.HasValue)
        {
            parent = _commentCommand.GetById(parentId.Value);
            if (parent == null || parent.story_id != storyId || parent.chapter_id != null)
                return StoryCommentPostOutcome.BadRequest("ParentId không hợp lệ (phải là comment cấp truyện).");
        }

        var entity = _commentCommand.AddStoryComment(storyId, userId, contentTrimmed, parentId);

        if (parent != null && parent.user_id.HasValue && parent.user_id != userId)
        {
            var replierName = entity.userNavigation?.user_profiles?.nickname?.Trim()
                              ?? entity.userNavigation?.email?.Trim()
                              ?? "Ai đó";
            try
            {
                var notif = NotificationDAO.NotifyCommentReply(parent.user_id.Value, replierName, storyId, story.title, entity.id);
                await PushCommentReplyNotificationAsync(notif, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "NotifyCommentReply failed for parent {ParentId}", parent.id);
            }
        }

        var dto = MapToStoryCommentDto(entity, userId, story.author_id);
        return StoryCommentPostOutcome.Ok(dto);
    }

    private async Task PushCommentReplyNotificationAsync(notifications n, CancellationToken cancellationToken)
    {
        if (n.user_id == null) return;
        try
        {
            await _notificationHubNotifier.NotifyUserAsync(n.user_id.Value, MapNotificationToDto(n));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Push COMMENT_REPLY notification failed. UserId={UserId} NotificationId={NotificationId}", n.user_id, n.id);
        }
    }

    private static NotificationDto MapNotificationToDto(notifications n) => new()
    {
        Id = n.id,
        Type = n.type,
        Title = n.title,
        Content = n.content,
        LinkUrl = n.link_url,
        IsRead = n.is_read == true,
        CreatedAt = n.created_at
    };

    private static string? ResolveCommentDisplayUserRole(string? accountRole, Guid? commentUserId, Guid? storyAuthorId)
    {
        var r = accountRole?.Trim();
        if (string.IsNullOrEmpty(r)) return null;
        if (string.Equals(r, "AUTHOR", StringComparison.OrdinalIgnoreCase))
        {
            if (!storyAuthorId.HasValue || !commentUserId.HasValue || commentUserId.Value != storyAuthorId.Value)
                return "USER";
        }

        return r;
    }

    private StoryCommentDto MapToStoryCommentDto(comments c, Guid? currentUserId, Guid? storyAuthorId)
    {
        var statusUpper = (c.status ?? "").Trim().ToUpperInvariant();
        var content = statusUpper == "HIDDEN_PARENT" ? "Nội dung bình luận đã bị ẩn." : (c.content ?? "");
        var nickname = c.userNavigation?.user_profiles?.nickname;
        var email = c.userNavigation?.email;
        var display = !string.IsNullOrWhiteSpace(nickname) ? nickname : email;

        var (userHasLiked, userReactionType, reactionCounts) = _reactionReader.GetSummary(c.id, currentUserId);

        return new StoryCommentDto
        {
            Id = c.id,
            StoryId = c.story_id ?? Guid.Empty,
            ParentId = c.parent_id,
            UserId = c.user_id ?? Guid.Empty,
            UserDisplayName = display,
            UserRole = ResolveCommentDisplayUserRole(c.userNavigation?.role, c.user_id, storyAuthorId),
            UserCreatedAt = c.userNavigation?.created_at,
            Content = content,
            LikesCount = c.likes_count ?? 0,
            UserHasLiked = userHasLiked,
            ReactionCounts = reactionCounts,
            UserReactionType = userReactionType,
            CreatedAt = c.created_at
        };
    }
}
