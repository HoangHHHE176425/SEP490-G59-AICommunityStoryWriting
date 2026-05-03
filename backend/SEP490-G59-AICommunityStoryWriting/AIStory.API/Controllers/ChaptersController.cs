using System;
using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using BusinessObjects;
using Services.DTOs.Chapters;
using Services.DTOs.Comments;
using Services.DTOs.Notifications;
using Services.DTOs.Stories;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/chapters")]
    public class ChaptersController : ControllerBase
    {
        /// <summary>Mã lỗi 409 khi xóa chương DRAFT còn version — khớp <c>ChapterService</c>.</summary>
        private const string ChapterDeleteVersionsConfirmCode = "CHAPTER_DELETE_VERSIONS_CONFIRM_REQUIRED";

        private readonly IChapterService _chapterService;
        private readonly IChapterVersionService _chapterVersionService;
        private readonly IStoryService _storyService;
        private readonly IContentGuardrailService _contentGuardrail;
        private readonly INotificationHubNotifier _notificationHubNotifier;
        private readonly ILogger<ChaptersController> _logger;

        public ChaptersController(
            IChapterService chapterService,
            IChapterVersionService chapterVersionService,
            IStoryService storyService,
            IContentGuardrailService contentGuardrail,
            INotificationHubNotifier notificationHubNotifier,
            ILogger<ChaptersController> logger)
        {
            _chapterService = chapterService;
            _chapterVersionService = chapterVersionService;
            _storyService = storyService;
            _contentGuardrail = contentGuardrail;
            _notificationHubNotifier = notificationHubNotifier;
            _logger = logger;
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

        private async Task PushCommentReplyNotificationAsync(notifications n)
        {
            if (n.user_id == null) return;
            try
            {
                await _notificationHubNotifier.NotifyUserAsync(n.user_id.Value, MapNotificationToDto(n));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push COMMENT_REPLY (chapter) failed. UserId={UserId} NotificationId={NotificationId}", n.user_id, n.id);
            }
        }

        /// <summary>Gửi thông báo + SignalR cho tác giả khi độc giả mở khóa chương trả phí (lỗi push không làm hỏng giao dịch unlock).</summary>
        private async Task PushAuthorChapterUnlockNotificationAsync(
            Guid authorId,
            Guid buyerUserId,
            ChapterResponseDto chapterDto,
            StoryResponseDto storyDto,
            int coinPrice,
            Guid chapterId)
        {
            try
            {
                var buyerName = NotificationDAO.GetUserDisplayName(buyerUserId);
                var notif = NotificationDAO.NotifyAuthorChapterUnlocked(
                    authorId,
                    buyerName,
                    storyDto.Title ?? "Truyện",
                    chapterDto.Title ?? "Chương",
                    coinPrice,
                    chapterDto.StoryId ?? Guid.Empty,
                    chapterId);
                await _notificationHubNotifier.NotifyUserAsync(authorId, MapNotificationToDto(notif));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push CHAPTER_UNLOCK notification failed. AuthorId={AuthorId} ChapterId={ChapterId}", authorId, chapterId);
            }
        }

        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        private bool CanBypassStoryComplianceHidden(StoryResponseDto? story, Guid? userId)
        {
            if (story == null || story.ComplianceHidden != true) return true;
            if (User.IsInRole("ADMIN") || User.IsInRole("COMPLIANCE")) return true;
            if (userId.HasValue && story.AuthorId.HasValue && story.AuthorId.Value == userId.Value) return true;
            return false;
        }

        private bool HasUnlockedPaidChapter(Guid userId, Guid chapterId)
        {
            using var db = new StoryPlatformDbContext();
            return db.purchases
                .AsNoTracking()
                .Any(p =>
                    p.user_id == userId &&
                    p.chapter_id == chapterId &&
                    ((p.escrow_status ?? string.Empty).ToUpper() == "RELEASED" || p.released_at != null));
        }

        /// <summary>Tạo chapter mới - Chỉ AUTHOR.</summary>
        [HttpPost]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult Create([FromBody] CreateChapterRequestDto request)
        {
            try
            {
                var authorId = GetCurrentUserId();
                if (!authorId.HasValue || authorId.Value == Guid.Empty)
                    return Unauthorized(new { message = "Không xác định được tài khoản tác giả. Vui lòng đăng nhập lại." });

                var chapter = _chapterService.Create(request, authorId.Value);
                return Created($"api/chapters/{chapter.Id}", new { message = "tạo truyện thành công", chapter });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while creating the chapter", error = ex.Message });
            }
        }

        /// <summary>Giống <see cref="StoriesController"/>: mặc định ẩn chapter của tác giả BANNED. Chỉ MODERATOR/ADMIN/COMPLIANCE đã đăng nhập mới được <c>excludeBannedStoryAuthors=false</c>.</summary>
        private void ApplyExcludeBannedStoryAuthorsPolicy(ChapterQueryDto query)
        {
            if (!Request.Query.Keys.Any(k => string.Equals(k, "excludeBannedStoryAuthors", StringComparison.OrdinalIgnoreCase)))
            {
                query.ExcludeBannedStoryAuthors = true;
                return;
            }
            if (query.ExcludeBannedStoryAuthors)
                return;
            var ok = User?.Identity?.IsAuthenticated == true &&
                     (User.IsInRole("MODERATOR") || User.IsInRole("ADMIN") || User.IsInRole("COMPLIANCE"));
            if (!ok)
                query.ExcludeBannedStoryAuthors = true;
        }

        /// <summary>Lấy danh sách chapters với pagination và filtering (cho phép xem không cần đăng nhập)</summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetAll([FromQuery] ChapterQueryDto query)
        {
            try
            {
                ApplyExcludeBannedStoryAuthorsPolicy(query);
                var result = _chapterService.GetAll(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching chapters", error = ex.Message });
            }
        }

        /// <summary>Lấy chapter theo ID (Guid) (bắt buộc đăng nhập để đọc)</summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        public IActionResult GetById(Guid id)
        {
            try
            {
                var chapter = _chapterService.GetById(id);
                if (chapter == null)
                    return NotFound(new { message = $"Chapter with ID {id} not found" });

                var userId = GetCurrentUserId();
                if (chapter.StoryId.HasValue)
                {
                    var stMeta = _storyService.GetById(chapter.StoryId.Value, userId);
                    if (stMeta == null)
                        return NotFound(new { message = $"Chapter with ID {id} not found" });
                    if (!CanBypassStoryComplianceHidden(stMeta, userId))
                        return NotFound(new { message = $"Chapter with ID {id} not found" });
                }

                if (chapter.StoryId.HasValue && userId.HasValue && userId.Value != Guid.Empty)
                {
                    var story = _storyService.GetById(chapter.StoryId.Value, userId);
                    var isAuthor = story?.AuthorId.HasValue == true && story.AuthorId.Value == userId.Value;
                    var accessType = chapter.AccessType?.ToUpper() ?? "FREE";
                    var coinPrice = chapter.CoinPrice ?? 0;

                    var unlocked = true;
                    if (string.Equals(accessType, "PAID", StringComparison.OrdinalIgnoreCase) && coinPrice > 0)
                    {
                        unlocked = isAuthor || HasUnlockedPaidChapter(userId.Value, id);
                        chapter.IsUnlocked = unlocked;
                        if (!unlocked)
                        {
                            // Locked chapter: vẫn trả metadata (title/order/giá) nhưng không trả content.
                            chapter.Content = null;
                            chapter.WordCount = null;
                            return Ok(chapter);
                        }
                    }

                    // Record đọc chỉ khi đã mở khóa hoặc chapter FREE.
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var ua = Request.Headers.UserAgent.ToString();
                    _storyService.RecordReadChapter(chapter.StoryId.Value, id, userId.Value, ip, ua);
                }

                if (chapter.AccessType?.ToUpper() == "PAID" && (chapter.CoinPrice ?? 0) > 0 && chapter.IsUnlocked == false)
                {
                    // Default fallback when we couldn't determine access yet.
                    chapter.IsUnlocked = true;
                }
                return Ok(chapter);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the chapter", error = ex.Message });
            }
        }

        /// <summary>Unlock chapter trả phí: trừ coin người mua + chia 70% platform / 30% author.</summary>
        [HttpPost("{id:guid}/unlock")]
        [Authorize]
        public async Task<IActionResult> UnlockPaidChapter(Guid id, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "User ID not found in token." });

            var chapter = _chapterService.GetById(id);
            if (chapter == null)
                return NotFound(new { message = "Chapter không tồn tại." });

            var accessType = chapter.AccessType?.ToUpper() ?? "FREE";
            var coinPrice = chapter.CoinPrice ?? 0;
            if (!string.Equals(accessType, "PAID", StringComparison.OrdinalIgnoreCase) || coinPrice <= 0)
                return BadRequest(new { message = "Chapter này không phải loại trả phí." });

            if (!chapter.StoryId.HasValue)
                return BadRequest(new { message = "Chapter thiếu StoryId." });

            var story = _storyService.GetById(chapter.StoryId.Value, userId);
            if (story?.AuthorId == null)
                return BadRequest(new { message = "Không xác định được author của truyện." });
            if (!CanBypassStoryComplianceHidden(story, userId))
                return NotFound(new { message = "Chapter không tồn tại." });

            var authorId = story.AuthorId.Value;
            var isAuthor = authorId == userId.Value;
            if (isAuthor)
                return Ok(new { unlocked = true, message = "Tác giả có quyền đọc miễn phí." });

            // Platform fee: 70% system, 30% author.
            var platformFee = (int)Math.Floor(coinPrice * 0.70m);
            platformFee = Math.Clamp(platformFee, 0, coinPrice);
            var authorNet = coinPrice - platformFee;

            await using var db = new StoryPlatformDbContext();
            var strategy = db.Database.CreateExecutionStrategy();
            return await strategy.ExecuteAsync(async () =>
            {
                await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
                try
                {
                    // Re-check inside transaction to avoid double-charging on race conditions.
                    var alreadyUnlocked = await db.purchases
                        .AsNoTracking()
                        .AnyAsync(p =>
                            p.user_id == userId.Value &&
                            p.chapter_id == id &&
                            ((p.escrow_status ?? string.Empty).ToUpper() == "RELEASED" || p.released_at != null),
                            cancellationToken);
                    if (alreadyUnlocked)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return Ok(new { unlocked = true, alreadyUnlocked = true });
                    }

                    // Ensure buyer wallet
                    var buyerWallet = await db.wallets.FirstOrDefaultAsync(w => w.user_id == userId.Value, cancellationToken);
                    if (buyerWallet == null)
                    {
                        buyerWallet = new wallets
                        {
                            user_id = userId.Value,
                            balance_coin = 0,
                            currency = "VND",
                            income_balance = 0m,
                            frozen_balance = 0m,
                            pending_escrow_balance = 0m,
                            updated_at = DateTime.UtcNow
                        };
                        db.wallets.Add(buyerWallet);
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    var buyerBalance = buyerWallet.balance_coin ?? 0;
                    if (buyerBalance < coinPrice)
                    {
                        await tx.RollbackAsync(cancellationToken);
                        return BadRequest(new { message = $"Số dư coin không đủ. Hiện {buyerBalance}, cần {coinPrice}." });
                    }

                    buyerWallet.balance_coin = buyerBalance - coinPrice;
                    buyerWallet.updated_at = DateTime.UtcNow;

                    // Ensure author wallet
                    var authorWallet = await db.wallets.FirstOrDefaultAsync(w => w.user_id == authorId, cancellationToken);
                    if (authorWallet == null)
                    {
                        authorWallet = new wallets
                        {
                            user_id = authorId,
                            balance_coin = 0,
                            currency = "VND",
                            income_balance = 0m,
                            frozen_balance = 0m,
                            pending_escrow_balance = 0m,
                            updated_at = DateTime.UtcNow
                        };
                        db.wallets.Add(authorWallet);
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    // Ensure platform wallet exists (id=1)
                    var platformWallet = await db.platform_wallet.FirstOrDefaultAsync(w => w.id == 1, cancellationToken);
                    if (platformWallet == null)
                    {
                        platformWallet = new platform_wallet
                        {
                            id = 1,
                            balance_coin = 0,
                            updated_at = DateTime.UtcNow
                        };
                        db.platform_wallet.Add(platformWallet);
                        await db.SaveChangesAsync(cancellationToken);
                    }

                    // Update wallets
                    platformWallet.balance_coin += platformFee;
                    platformWallet.updated_at = DateTime.UtcNow;

                    var receiverIncome = authorWallet.income_balance ?? 0m;
                    authorWallet.income_balance = receiverIncome + authorNet;
                    authorWallet.updated_at = DateTime.UtcNow;

                    // Create purchase record (history)
                    var purchase = new purchases
                    {
                        id = Guid.NewGuid(),
                        user_id = userId.Value,
                        story_id = chapter.StoryId,
                        chapter_id = id,
                        price_paid = coinPrice,
                        purchase_type = "CHAPTER_UNLOCK",
                        escrow_status = "RELEASED",
                        released_at = DateTime.UtcNow,
                        // DB type is decimal(5,2); store platform fee as percent with 2 decimals.
                        platform_fee_ratio = 70.00m,
                        created_at = DateTime.UtcNow
                    };
                    db.purchases.Add(purchase);

                    // Log author income for analytics/withdraw flows
                    db.author_income_logs.Add(new author_income_logs
                    {
                        author_id = authorId,
                        source_type = "CHAPTER_UNLOCK",
                        source_id = purchase.id,
                        gross_amount = coinPrice,
                        platform_fee = platformFee,
                        net_amount = authorNet,
                        status = "AVAILABLE",
                        created_at = DateTime.UtcNow
                    });

                    await db.SaveChangesAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);

                    await PushAuthorChapterUnlockNotificationAsync(authorId, userId.Value, chapter, story, coinPrice, id);

                    return Ok(new { unlocked = true });
                }
                catch (Exception ex)
                {
                    try { await tx.RollbackAsync(cancellationToken); } catch { /* ignore */ }
                    return StatusCode(500, new { message = "Unlock chapter failed.", error = ex.Message });
                }
            });
        }

        /// <summary>Lấy tất cả chapters của một story (Guid) (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("story/{storyId:guid}")]
        [AllowAnonymous]
        public IActionResult GetByStoryId(Guid storyId)
        {
            try
            {
                var chapters = _chapterService.GetByStoryId(storyId);
                return Ok(chapters);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching chapters", error = ex.Message });
            }
        }

        /// <summary>Lấy chapter theo story ID (Guid) và order index (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("story/{storyId:guid}/order/{orderIndex:int}")]
        [AllowAnonymous]
        public IActionResult GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex)
        {
            try
            {
                var chapter = _chapterService.GetByStoryIdAndOrderIndex(storyId, orderIndex);
                return chapter == null ? NotFound(new { message = $"Chapter with order index {orderIndex} not found for story {storyId}" }) : Ok(chapter);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the chapter", error = ex.Message });
            }
        }

        /// <summary>Lấy comment của chapter (cho phép xem không cần đăng nhập).</summary>
        [HttpGet("{id:guid}/comments")]
        [AllowAnonymous]
        public IActionResult GetChapterComments(Guid id)
        {
            try
            {
                var chapter = _chapterService.GetById(id);
                if (chapter == null)
                    return NotFound(new { message = "Chapter not found." });
                Guid? storyAuthorId = null;
                if (chapter.StoryId.HasValue)
                {
                    var st = StoryDAO.GetById(chapter.StoryId.Value);
                    storyAuthorId = st?.author_id;
                }
                var entities = CommentDAO.GetChapterCommentsForDisplay(id);
                var currentUserId = GetCurrentUserId();
                var dtos = entities.Select(c => MapToStoryCommentDto(c, currentUserId, storyAuthorId)).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching chapter comments", error = ex.Message });
            }
        }

        /// <summary>Comment chapter. Bắt buộc đăng nhập và đã đọc ít nhất 1 chapter của truyện.</summary>
        [HttpPost("{id:guid}/comments")]
        [Authorize]
        public async Task<IActionResult> AddChapterComment(Guid id, [FromBody] CreateStoryCommentRequestDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });
                if (request == null || string.IsNullOrWhiteSpace(request.Content))
                    return BadRequest(new { message = "Nội dung comment không được để trống." });
                var content = request.Content.Trim();
                if (content.Length > 2000)
                    return BadRequest(new { message = "Nội dung comment tối đa 2000 ký tự." });

                var guardrailResult = await _contentGuardrail.CheckCommentBannedWordsAsync(content, HttpContext.RequestAborted);
                if (!guardrailResult.Passed)
                    return BadRequest(new
                    {
                        message = "Nội dung comment chứa từ không được phép.",
                        violations = guardrailResult.Violations.Select(v => new { v.Type, v.Quote })
                    });

                var chapter = _chapterService.GetById(id);
                if (chapter == null || !chapter.StoryId.HasValue)
                    return NotFound(new { message = "Chapter not found." });
                var storyId = chapter.StoryId.Value;
                var story = StoryDAO.GetById(storyId);
                if (story == null || !string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Chỉ có thể comment chapter của truyện đã PUBLISHED." });
                if (story.comments_disabled)
                    return BadRequest(new
                    {
                        message = "Truyện này đang trong quá trình xử lý vi phạm nên hiện không thể bình luận."
                    });
                if (!UserActivityLogDAO.HasReadAnyChapterOfStory(userId.Value, storyId))
                    return BadRequest(new { message = "Bạn cần đọc ít nhất một chapter của truyện trước khi comment." });

                comments? parent = null;
                if (request.ParentId.HasValue)
                {
                    parent = CommentDAO.GetById(request.ParentId.Value);
                    if (parent == null || parent.chapter_id != id)
                        return BadRequest(new { message = "ParentId không hợp lệ (phải là comment của chapter này)." });
                }

                var entity = CommentDAO.AddChapterComment(storyId, id, userId.Value, content, request.ParentId);
                if (parent != null && parent.user_id.HasValue && parent.user_id != userId.Value)
                {
                    var replierName = entity.userNavigation?.user_profiles?.nickname?.Trim()
                        ?? entity.userNavigation?.email?.Trim() ?? "Ai đó";
                    try
                    {
                        var chapterCommentLink = $"/chapter?storyId={storyId}&chapterId={id}#comment-{entity.id}";
                        var chapterDescriptor = $"Chương {chapter.OrderIndex}" +
                            (string.IsNullOrWhiteSpace(chapter.Title) ? "" : $" «{chapter.Title.Trim()}»");
                        var notif = NotificationDAO.NotifyCommentReply(parent.user_id.Value, replierName, storyId, story.title, entity.id, chapterCommentLink, chapterDescriptor);
                        await PushCommentReplyNotificationAsync(notif);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "NotifyCommentReply (chapter) failed for parent {ParentId}", parent.id);
                    }
                }
                var dto = MapToStoryCommentDto(entity, userId, story.author_id);
                return Created($"/api/chapters/{id}/comments/{dto.Id}", dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while adding chapter comment", error = ex.Message });
            }
        }

        /// <summary>Lấy danh sách người đã reaction comment của chapter.</summary>
        [HttpGet("{chapterId:guid}/comments/{commentId:guid}/reactions")]
        [AllowAnonymous]
        public IActionResult GetChapterCommentReactions(Guid chapterId, Guid commentId)
        {
            try
            {
                var comment = CommentDAO.GetById(commentId);
                if (comment == null || comment.chapter_id != chapterId)
                    return NotFound(new { message = "Comment not found or not belong to this chapter." });
                var list = CommentDAO.GetCommentReactions(commentId);
                var dtos = list.Select(x => new CommentReactionUserDto
                {
                    UserId = x.UserId,
                    UserDisplayName = x.DisplayName,
                    ReactionType = x.ReactionType
                }).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching comment reactions", error = ex.Message });
            }
        }

        /// <summary>Đặt reaction cho comment của chapter: LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW.</summary>
        [HttpPost("{chapterId:guid}/comments/{commentId:guid}/reaction")]
        [Authorize]
        public IActionResult SetChapterCommentReaction(Guid chapterId, Guid commentId, [FromBody] SetCommentReactionRequestDto? request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });
                var comment = CommentDAO.GetById(commentId);
                if (comment == null || comment.chapter_id != chapterId)
                    return NotFound(new { message = "Comment not found or not belong to this chapter." });
                var storyId = comment.story_id;
                var story = storyId.HasValue ? StoryDAO.GetById(storyId.Value) : null;
                var reactionType = request?.ReactionType;
                var newType = CommentDAO.SetReaction(userId.Value, commentId, reactionType);
                if (!string.IsNullOrWhiteSpace(newType) && comment.user_id.HasValue && comment.user_id != userId.Value)
                {
                    try
                    {
                        var actorName = NotificationDAO.GetUserDisplayName(userId.Value);
                        NotificationDAO.NotifyCommentReaction(comment.user_id.Value, actorName, storyId ?? Guid.Empty, story?.title, newType);
                    }
                    catch { /* best effort */ }
                }
                var counts = CommentDAO.GetReactionCounts(commentId);
                return Ok(new { userReactionType = newType, reactionCounts = counts });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while setting reaction", error = ex.Message });
            }
        }

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

        private static StoryCommentDto MapToStoryCommentDto(
            comments c,
            Guid? currentUserId = null,
            Guid? storyAuthorId = null)
        {
            var statusUpper = (c.status ?? "").Trim().ToUpperInvariant();
            var content = statusUpper == "HIDDEN_PARENT" ? "Nội dung bình luận đã bị ẩn." : (c.content ?? "");
            var nickname = c.userNavigation?.user_profiles?.nickname;
            var email = c.userNavigation?.email;
            var display = !string.IsNullOrWhiteSpace(nickname) ? nickname : email;
            var userHasLiked = false;
            IReadOnlyDictionary<string, int>? reactionCounts = null;
            string? userReactionType = null;
            try
            {
                if (currentUserId.HasValue)
                {
                    userHasLiked = CommentDAO.HasLiked(currentUserId.Value, c.id);
                    userReactionType = CommentDAO.GetUserReaction(currentUserId.Value, c.id);
                }
                reactionCounts = CommentDAO.GetReactionCounts(c.id);
            }
            catch
            {
                reactionCounts = new Dictionary<string, int>();
            }
            return new StoryCommentDto
            {
                Id = c.id,
                StoryId = c.story_id ?? Guid.Empty,
                ParentId = c.parent_id,
                UserId = c.user_id ?? Guid.Empty,
                UserDisplayName = display,
                UserAvatarUrl = c.userNavigation?.user_profiles?.avatar_url,
                UserRole = ResolveCommentDisplayUserRole(c.userNavigation?.role, c.user_id, storyAuthorId),
                UserCreatedAt = c.userNavigation?.created_at,
                Content = content,
                LikesCount = c.likes_count ?? 0,
                UserHasLiked = userHasLiked,
                ReactionCounts = reactionCounts ?? new Dictionary<string, int>(),
                UserReactionType = userReactionType,
                CreatedAt = c.created_at
            };
        }

        /// <summary>Cập nhật chapter - Chỉ AUTHOR (chỉ được sửa chapter của chính mình)</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult Update(Guid id, [FromBody] UpdateChapterRequestDto request)
        {
            try
            {
                var updated = _chapterService.Update(id, request);
                return updated ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while updating the chapter", error = ex.Message });
            }
        }

        /// <summary>Xóa chapter (chỉ DRAFT). Nếu có phiên bản (chapter_versions), lần đầu trả 409 với code CHAPTER_DELETE_VERSIONS_CONFIRM_REQUIRED; gọi lại với deleteIncludingVersions=true sau khi user xác nhận. Nội dung ai_generated_content của chương cũng bị xóa khi xóa thành công.</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult Delete(Guid id, [FromQuery] bool deleteIncludingVersions = false)
        {
            try
            {
                var deleted = _chapterService.Delete(id, deleteIncludingVersions);
                return deleted ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Data["ErrorCode"]?.ToString() == ChapterDeleteVersionsConfirmCode
                    && ex.Data["VersionCount"] != null)
                {
                    var vc = Convert.ToInt32(ex.Data["VersionCount"]);
                    return Conflict(new
                    {
                        code = ChapterDeleteVersionsConfirmCode,
                        versionCount = vc,
                        message = ex.Message
                    });
                }
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the chapter", error = ex.Message });
            }
        }

        /// <summary>Publish chapter - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/publish")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult Publish(Guid id)
        {
            try
            {
                var published = _chapterService.Publish(id);
                return published ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while publishing the chapter", error = ex.Message });
            }
        }

        /// <summary>Unpublish chapter - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/unpublish")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult Unpublish(Guid id)
        {
            try
            {
                var unpublished = _chapterService.Unpublish(id);
                return unpublished ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while unpublishing the chapter", error = ex.Message });
            }
        }

        /// <summary>Sắp xếp lại thứ tự chapter - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/reorder")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult Reorder(Guid id, [FromBody] int newOrderIndex)
        {
            try
            {
                var reordered = _chapterService.Reorder(id, newOrderIndex);
                return reordered ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while reordering the chapter", error = ex.Message });
            }
        }

        /// <summary>Xem lý do từ chối chapter - Chỉ AUTHOR (chỉ chapter thuộc truyện của mình).</summary>
        [HttpGet("{id:guid}/rejection-reason")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult GetRejectionReason(Guid id)
        {
            try
            {
                var chapter = _chapterService.GetById(id);
                if (chapter == null)
                    return NotFound(new { message = "Chapter không tồn tại." });
                if (!chapter.StoryId.HasValue)
                    return Forbid();
                var story = _storyService.GetById(chapter.StoryId.Value);
                if (story == null)
                    return Forbid();
                var authorIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
                if (authorIdClaim == null || !Guid.TryParse(authorIdClaim.Value, out var currentUserId) || story.AuthorId != currentUserId)
                    return Forbid();
                if (chapter.Status == "PUBLISHED")
                    return Ok(new { reason = (string?)null, rejectedAt = (DateTime?)null });
                var (reason, rejectedAt) = _chapterService.GetLatestRejectionForChapter(id);
                return Ok(new { reason, rejectedAt });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi lấy lý do từ chối", error = ex.Message });
            }
        }

        // ---------- Chapter Versions (AUTHOR) ----------
        /// <summary>Lấy danh sách version của chapter. Chỉ AUTHOR. Bao gồm cả version đã xuất bản (PUBLISHED) — vẫn hiển thị trong danh sách, không ẩn/xóa.</summary>
        [HttpGet("{chapterId:guid}/versions")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult GetChapterVersions(Guid chapterId)
        {
            var list = _chapterVersionService.GetByChapterId(chapterId).ToList();
            return Ok(list);
        }

        /// <summary>Lấy chi tiết một version. Chỉ AUTHOR.</summary>
        [HttpGet("{chapterId:guid}/versions/{versionId:guid}")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult GetChapterVersion(Guid chapterId, Guid versionId)
        {
            var v = _chapterVersionService.GetById(versionId);
            if (v == null || v.ChapterId != chapterId)
                return NotFound(new { message = "Version không tồn tại." });
            return Ok(v);
        }

        /// <summary>Tạo version mới cho chapter. Chỉ AUTHOR.</summary>
        [HttpPost("{chapterId:guid}/versions")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult CreateChapterVersion(Guid chapterId, [FromBody] CreateChapterVersionRequestDto request)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var v = _chapterVersionService.Create(chapterId, authorId.Value, request ?? new CreateChapterVersionRequestDto());
                return v == null ? NotFound(new { message = "Chapter không tồn tại." }) : CreatedAtAction(nameof(GetChapterVersion), new { chapterId, versionId = v.Id }, v);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>Cập nhật version (chỉ DRAFT). Chỉ AUTHOR.</summary>
        [HttpPut("{chapterId:guid}/versions/{versionId:guid}")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult UpdateChapterVersion(Guid chapterId, Guid versionId, [FromBody] UpdateChapterVersionRequestDto request)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            if (request == null)
                return BadRequest();
            try
            {
                var ok = _chapterVersionService.Update(versionId, authorId.Value, request);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Xóa version (chỉ DRAFT). Chỉ AUTHOR.</summary>
        [HttpDelete("{chapterId:guid}/versions/{versionId:guid}")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult DeleteChapterVersion(Guid chapterId, Guid versionId)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var ok = _chapterVersionService.Delete(versionId, authorId.Value);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại hoặc không thể xóa." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Gửi duyệt version: áp dụng nội dung version lên chapter và chuyển chapter sang PENDING_REVIEW. Chỉ AUTHOR.</summary>
        [HttpPost("{chapterId:guid}/versions/{versionId:guid}/submit")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult SubmitChapterVersion(Guid chapterId, Guid versionId)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var ok = _chapterVersionService.SubmitForReview(versionId, authorId.Value);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại hoặc không thể gửi duyệt." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Hủy gửi duyệt version: đưa version và chapter về DRAFT. Chỉ AUTHOR, chỉ version PENDING_REVIEW.</summary>
        [HttpPost("{chapterId:guid}/versions/{versionId:guid}/unsubmit")]
        [Authorize(Policy = "AuthorStrict")]
        public IActionResult UnsubmitChapterVersion(Guid chapterId, Guid versionId)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var ok = _chapterVersionService.CancelSubmit(versionId, authorId.Value);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại hoặc không thể hủy gửi duyệt." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

    }
}