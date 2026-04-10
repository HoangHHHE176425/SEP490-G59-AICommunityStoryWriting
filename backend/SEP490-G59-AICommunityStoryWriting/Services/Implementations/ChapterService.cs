using System;
using System.Collections.Generic;
using System.Linq;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repositories;
using Services.DTOs.Chapters;
using Services.DTOs.Notifications;
using Services.DTOs.Stories;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ChapterService : IChapterService
    {
        /// <summary>Gắn vào <see cref="InvalidOperationException.Data"/> khi cần client xác nhận xóa kèm version (409).</summary>
        internal const string DeleteRequiresVersionsConfirmationCode = "CHAPTER_DELETE_VERSIONS_CONFIRM_REQUIRED";

        private readonly IChapterRepository _chapterRepository;
        private readonly IChapterVersionRepository _versionRepository;
        private readonly IAiGeneratedContentRepository _aiContentRepository;
        private readonly IUserLookup _userLookup;
        private readonly IStoryLookup _storyLookup;
        private readonly IServiceScopeFactory? _scopeFactory;
        private readonly IModerationHubNotifier? _moderationHubNotifier;
        private readonly INotificationHubNotifier? _notificationHubNotifier;
        private readonly ILogger<ChapterService> _logger;

        public ChapterService(
            IChapterRepository chapterRepository,
            IChapterVersionRepository versionRepository,
            IAiGeneratedContentRepository aiContentRepository,
            IUserLookup userLookup,
            IStoryLookup storyLookup,
            ILogger<ChapterService> logger,
            IModerationHubNotifier? moderationHubNotifier = null,
            INotificationHubNotifier? notificationHubNotifier = null)
        {
            _chapterRepository = chapterRepository;
            _versionRepository = versionRepository;
            _aiContentRepository = aiContentRepository;
            _userLookup = userLookup;
            _storyLookup = storyLookup;
            _logger = logger;
            _moderationHubNotifier = moderationHubNotifier;
            _notificationHubNotifier = notificationHubNotifier;
        }

        // Overload for DI setups that also provide IServiceScopeFactory.
        public ChapterService(
            IChapterRepository chapterRepository,
            IChapterVersionRepository versionRepository,
            IAiGeneratedContentRepository aiContentRepository,
            IServiceScopeFactory scopeFactory,
            IUserLookup userLookup,
            IStoryLookup storyLookup,
            ILogger<ChapterService> logger,
            IModerationHubNotifier? moderationHubNotifier = null,
            INotificationHubNotifier? notificationHubNotifier = null)
            : this(chapterRepository, versionRepository, aiContentRepository, userLookup, storyLookup, logger, moderationHubNotifier, notificationHubNotifier)
        {
            _scopeFactory = scopeFactory;
        }

        /// <inheritdoc cref="IChapterService.Create"/>
        public ChapterResponseDto Create(CreateChapterRequestDto request, Guid authorId)
        {
            if (request == null) throw new ArgumentNullException(nameof(request));
            if (authorId == Guid.Empty)
                throw new ArgumentException("Author ID is required.", nameof(authorId));

            var story = _storyLookup.GetById(request.StoryId);
            if (story == null)
            {
                throw new InvalidOperationException($"Story with ID {request.StoryId} not found.");
            }

            // UTCID13/14: chỉ story.author_id được tạo chương; tác giả khác (dù có truyện khác) không được.
            if (!story.author_id.HasValue || story.author_id.Value != authorId)
                throw new UnauthorizedAccessException("Bạn không phải tác giả của truyện này.");

            return CreateChapterCore(request, story);
        }

        private ChapterResponseDto CreateChapterCore(CreateChapterRequestDto request, stories story)
        {
            if (request.Id == Guid.Empty)
                throw new ArgumentException("Id must be a non-empty Guid (do not leave empty).");

            if (story.author_id is Guid aid && _userLookup.IsAuthorWritingSuspended(aid))
                throw new InvalidOperationException("Tác giả đang bị tạm khóa chức năng viết truyện/chương (compliance/admin).");
            EnsureStoryProgressAllowsChapterWrite(story, "tạo chương");

            var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(request.StoryId, request.OrderIndex);
            if (existingChapter != null)
            {
                throw new InvalidOperationException($"Chapter with order index {request.OrderIndex} already exists for this story.");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new ArgumentException("Tiêu đề chương là bắt buộc và không được chỉ gồm khoảng trắng.");

            EnsureUniqueChapterTitleForStory(request.StoryId, request.Title, null);

            var validAccessTypes = new[] { "FREE", "PAID" };
            var accessType = request.AccessType?.ToUpper() ?? "FREE";
            if (!string.IsNullOrWhiteSpace(request.AccessType) && !validAccessTypes.Contains(accessType))
            {
                throw new ArgumentException($"Invalid access type. Must be one of: {string.Join(", ", validAccessTypes)}");
            }

            // Validate coin price based on access type
            var coinPrice = request.CoinPrice ?? 0;
            if (accessType == "PAID" && coinPrice <= 0)
            {
                throw new ArgumentException("Coin price must be greater than 0 for PAID chapters.");
            }
            if (accessType == "PAID" && (story.total_views ?? 0) < 500)
            {
                throw new InvalidOperationException("Truyện cần tối thiểu 500 lượt xem mới được thiết lập chế độ trả phí cho chương.");
            }
            if (accessType == "FREE" && coinPrice > 0)
                throw new ArgumentException("Chương miễn phí (FREE) không được khai báo giá coin lớn hơn 0.");

            var content = request.Content;
            if (request.AiGeneratedContentId.HasValue)
            {
                var aiDraft = _aiContentRepository.GetById(request.AiGeneratedContentId.Value);
                if (aiDraft != null && aiDraft.story_id == request.StoryId && !string.IsNullOrWhiteSpace(aiDraft.ai_output))
                {
                    if (string.IsNullOrWhiteSpace(content))
                        content = aiDraft.ai_output;
                }
            }

            if (string.IsNullOrWhiteSpace(content))
                throw new InvalidOperationException("Vui lòng điền đầy đủ thông tin.");

            const int minChapterContentChars = 500;
            if (content.Length < minChapterContentChars)
                throw new InvalidOperationException("Nội dung chương quá ngắn: yêu cầu tối thiểu 500 ký tự.");

            var wordCount = CalculateWordCount(content);

            // Determine status - default to DRAFT if not specified or invalid
            var status = "DRAFT";
            var publishedAt = (DateTime?)null;
            var validStatuses = new[] { "DRAFT", "PENDING_REVIEW", "REJECTED", "PUBLISHED", "HIDDEN", "ARCHIVED" };
            if (!string.IsNullOrWhiteSpace(request.Status) && validStatuses.Contains(request.Status.ToUpper()))
            {
                status = request.Status.ToUpper();
                if (status == "PUBLISHED")
                {
                    publishedAt = DateTime.Now;
                }
            }

            var chapter = new chapters
            {
                id = request.Id,
                story_id = request.StoryId,
                title = request.Title,
                content = content,
                order_index = request.OrderIndex,
                status = status,
                access_type = accessType,
                coin_price = coinPrice,
                word_count = wordCount,
                ai_contribution_ratio = request.AiContributionRatio ?? 0,
                is_ai_clean = request.IsAiClean,
                published_at = publishedAt,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            if (request.AiSimilarityPercent.HasValue)
                chapter.ai_similarity_percent = Math.Round(request.AiSimilarityPercent.Value, 2);

            _chapterRepository.Add(chapter);

            // Nếu trước đó FE đã dùng draft_chapter_id để lưu AI gợi ý,
            // map toàn bộ record draft sang chapter thật ngay khi tạo xong.
            _aiContentRepository.BindDraftChapterId(chapter.id, chapter.id, chapter.order_index);

            if (request.AiGeneratedContentId.HasValue)
                _aiContentRepository.UpdateChapterId(request.AiGeneratedContentId.Value, chapter.id, chapter.order_index);

            try
            {
                UpdateStoryChapterStats(request.StoryId);

                // If chapter is published, update story's last_published_at and notify followers (DB + real-time)
                if (status == "PUBLISHED" && story != null)
                {
                    story.last_published_at = DateTime.Now;
                    _storyLookup.Update(story);
                    Console.WriteLine($"[CONSOLE] ChapterService.Create PUBLISHED -> NotifyStoryFollowersNewChapter StoryId={request.StoryId} ChapterId={chapter.id}");
                    _logger.LogInformation("ChapterService.Create calling NotifyStoryFollowersNewChapter StoryId={StoryId} ChapterId={ChapterId}", request.StoryId, chapter.id);
                    var createdNotifications = NotificationDAO.NotifyStoryFollowersNewChapter(request.StoryId, chapter.id, request.Title, story.title, _logger);
                    _ = PushNotificationsToFollowersAsync(createdNotifications);
                    if (story.author_id.HasValue)
                    {
                        var authorNotifications = NotificationDAO.NotifyAuthorFollowersNewChapter(story.author_id.Value, request.StoryId, chapter.id, request.Title, story.title, _logger);
                        _ = PushNotificationsToFollowersAsync(authorNotifications);
                    }
                }
            }
            catch (Exception)
            {
                // Log error but don't fail the create operation
                // The chapter was already created successfully
            }

            if (string.Equals(status, "PUBLISHED", StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(content))
            {
                ChapterMemoryAnalysisScheduler.TrySchedule(
                    _scopeFactory,
                    _logger,
                    request.StoryId,
                    chapter.id,
                    chapter.title,
                    chapter.order_index,
                    content);
            }

            // Create: return lightweight DTO without extra DB lookups.
            return MapToResponseDto(chapter, includeStoryLookup: false, storyTitleOverride: story.title);
        }

        public PagedResultDto<ChapterListItemDto> GetAll(ChapterQueryDto query)
        {
            var chaptersQuery = _chapterRepository.GetAll();

            if (query.StoryId.HasValue)
            {
                chaptersQuery = chaptersQuery.Where(c => c.story_id == query.StoryId.Value);
            }

            if (query.StoryIds != null && query.StoryIds.Count > 0)
            {
                var ids = query.StoryIds;
                chaptersQuery = chaptersQuery.Where(c => c.story_id.HasValue && ids.Contains(c.story_id.Value));
            }

            if (query.ExcludeChapterIds != null && query.ExcludeChapterIds.Count > 0)
            {
                var excludeIds = query.ExcludeChapterIds;
                chaptersQuery = chaptersQuery.Where(c => !excludeIds.Contains(c.id));
            }

            if (query.IncludeChapterIds != null && query.IncludeChapterIds.Count > 0)
            {
                var includeIds = query.IncludeChapterIds;
                chaptersQuery = chaptersQuery.Where(c => includeIds.Contains(c.id));
            }

            if (query.ExcludeBannedStoryAuthors)
            {
                chaptersQuery = chaptersQuery.Where(c =>
                    c.story == null
                    || c.story.author == null
                    || c.story.author.status == null
                    || c.story.author.status.ToUpper() != "BANNED");
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.Trim().ToLower();
                chaptersQuery = chaptersQuery.Where(c =>
                    (c.title != null && c.title.ToLower().Contains(searchLower)) ||
                    (c.story != null && c.story.title != null && c.story.title.ToLower().Contains(searchLower)));
            }

            if (query.PendingVersionChapterIds != null && query.PendingVersionChapterIds.Count > 0)
            {
                var ids = query.PendingVersionChapterIds;
                // Hiển thị chapter chờ duyệt: (1) chapter gốc PENDING_REVIEW hoặc (2) chapter có ít nhất một version PENDING_REVIEW (kể cả chapter đang DRAFT).
                chaptersQuery = chaptersQuery.Where(c =>
                    c.status == "PENDING_REVIEW" ||
                    ids.Contains(c.id));
            }
            else if (query.StatusIn != null && query.StatusIn.Count > 0)
            {
                var statusList = query.StatusIn;
                chaptersQuery = chaptersQuery.Where(c => c.status != null && statusList.Contains(c.status));
            }
            else if (!string.IsNullOrWhiteSpace(query.Status))
            {
                chaptersQuery = chaptersQuery.Where(c => c.status == query.Status);
            }

            if (!string.IsNullOrWhiteSpace(query.AccessType))
            {
                chaptersQuery = chaptersQuery.Where(c => c.access_type == query.AccessType);
            }

            chaptersQuery = query.SortBy?.ToLower() switch
            {
                "created_at" => query.SortOrder == "asc"
                    ? chaptersQuery.OrderBy(c => c.created_at)
                    : chaptersQuery.OrderByDescending(c => c.created_at),
                "updated_at" => query.SortOrder == "asc"
                    ? chaptersQuery.OrderBy(c => c.updated_at)
                    : chaptersQuery.OrderByDescending(c => c.updated_at),
                "published_at" => query.SortOrder == "asc"
                    ? chaptersQuery.OrderBy(c => c.published_at)
                    : chaptersQuery.OrderByDescending(c => c.published_at),
                "title" => query.SortOrder == "asc"
                    ? chaptersQuery.OrderBy(c => c.title ?? "")
                    : chaptersQuery.OrderByDescending(c => c.title ?? ""),
                _ => query.SortOrder == "asc"
                    ? chaptersQuery.OrderBy(c => c.order_index)
                    : chaptersQuery.OrderByDescending(c => c.order_index)
            };

            var totalCount = chaptersQuery.Count();

            var chapterList = chaptersQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            var storyIds = chapterList.Where(c => c.story_id.HasValue).Select(c => c.story_id!.Value).Distinct().ToList();
            var storyTitles = new Dictionary<Guid, string>();
            foreach (var sid in storyIds)
            {
                var story = _storyLookup.GetById(sid);
                if (story != null)
                    storyTitles[sid] = story.title ?? "";
            }

            var items = chapterList.Select(c =>
            {
                var dto = MapToListItemDto(c, c.story_id.HasValue ? storyTitles.GetValueOrDefault(c.story_id.Value) : null);
                if (string.Equals(c.status, "REJECTED", StringComparison.OrdinalIgnoreCase))
                {
                    var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("CHAPTER", c.id);
                    dto.RejectionReason = reason;
                    dto.RejectedAt = rejectedAt;
                }
                return dto;
            }).ToList();

            ApplyChapterCommentCounts(items, chapterList.Select(c => c.id));

            EnrichChapterListItemsWithReviewSla(chapterList, items);
            EnrichModeratorRejectionHistoryForChapterList(chapterList, items);

            return new PagedResultDto<ChapterListItemDto>
            {
                Items = items,
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public ChapterResponseDto? GetById(Guid id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null) return null;
            var dto = MapToResponseDto(chapter);
            if (chapter.status == "REJECTED")
            {
                var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("CHAPTER", id);
                dto.RejectionReason = reason;
                dto.RejectedAt = rejectedAt;
            }
            return dto;
        }

        public (string? reason, DateTime? rejectedAt) GetLatestRejectionForChapter(Guid chapterId)
        {
            return DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("CHAPTER", chapterId);
        }

        public IEnumerable<ChapterListItemDto> GetByStoryId(Guid storyId)
        {
            var chapterList = _chapterRepository.GetByStoryId(storyId)
                .OrderBy(c => c.order_index)
                .ToList();

            var storyTitle = _storyLookup.GetById(storyId)?.title;
            var items = chapterList.Select(c =>
            {
                var dto = MapToListItemDto(c, storyTitle);
                if (string.Equals(c.status, "REJECTED", StringComparison.OrdinalIgnoreCase))
                {
                    var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("CHAPTER", c.id);
                    dto.RejectionReason = reason;
                    dto.RejectedAt = rejectedAt;
                }
                return dto;
            }).ToList();
            ApplyChapterCommentCounts(items, chapterList.Select(c => c.id));
            EnrichChapterListItemsWithReviewSla(chapterList, items);
            EnrichModeratorRejectionHistoryForChapterList(chapterList, items);
            return items;
        }

        public ChapterResponseDto? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex)
        {
            var chapter = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, orderIndex);
            if (chapter == null) return null;
            var dto = MapToResponseDto(chapter);
            if (chapter.status == "REJECTED")
            {
                var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("CHAPTER", chapter.id);
                dto.RejectionReason = reason;
                dto.RejectedAt = rejectedAt;
            }
            dto.CommentCount = CommentDAO.GetApprovedCommentCountByChapterId(chapter.id);
            return dto;
        }

        public bool Update(Guid id, UpdateChapterRequestDto request)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;
            var storyForUpdate = _storyLookup.GetById(chapter.story_id ?? Guid.Empty);
            EnsureStoryAuthorNotWritingSuspended(storyForUpdate);
            EnsureStoryProgressAllowsChapterWrite(storyForUpdate, "chỉnh sửa chương");

            var previousStatus = chapter.status?.ToUpperInvariant() ?? "DRAFT";

            if (request.OrderIndex.HasValue && request.OrderIndex.Value != chapter.order_index)
            {
                var storyId = chapter.story_id ?? Guid.Empty;
                var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, request.OrderIndex.Value);
                if (existingChapter != null && existingChapter.id != id)
                {
                    throw new InvalidOperationException($"Chapter with order index {request.OrderIndex.Value} already exists for this story.");
                }
            }

            var targetStoryId = chapter.story_id ?? Guid.Empty;
            EnsureUniqueChapterTitleForStory(targetStoryId, request.Title, id);

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var validStatuses = new[] { "DRAFT", "PENDING_REVIEW", "REJECTED", "PUBLISHED", "HIDDEN", "ARCHIVED" };
                if (!validStatuses.Contains(request.Status.ToUpper()))
                {
                    throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                }
            }

            if (!string.IsNullOrWhiteSpace(request.AccessType))
            {
                var validAccessTypes = new[] { "FREE", "PAID" };
                var accessType = request.AccessType.ToUpper();
                if (!validAccessTypes.Contains(accessType))
                {
                    throw new ArgumentException($"Invalid access type. Must be one of: {string.Join(", ", validAccessTypes)}");
                }

                // Validate coin price based on access type
                var coinPrice = request.CoinPrice ?? chapter.coin_price ?? 0;
                if (accessType == "PAID" && coinPrice <= 0)
                {
                    throw new ArgumentException("Coin price must be greater than 0 for PAID chapters.");
                }
                if (accessType == "PAID" && !string.Equals(chapter.access_type, "PAID", StringComparison.OrdinalIgnoreCase))
                {
                    var story = targetStoryId == Guid.Empty ? null : _storyLookup.GetById(targetStoryId);
                    if ((story?.total_views ?? 0) < 500)
                        throw new InvalidOperationException("Truyện cần tối thiểu 500 lượt xem mới được thiết lập chế độ trả phí cho chương.");
                }
                if (accessType == "FREE")
                {
                    coinPrice = 0; // Force coin price to 0 for FREE chapters
                }

                chapter.access_type = accessType;
                chapter.coin_price = coinPrice;
            }
            else if (request.CoinPrice.HasValue)
            {
                // If only coin price is updated, validate based on current access type
                var currentAccessType = chapter.access_type?.ToUpper() ?? "FREE";
                var coinPrice = request.CoinPrice.Value;
                if (currentAccessType == "PAID" && coinPrice <= 0)
                {
                    throw new ArgumentException("Coin price must be greater than 0 for PAID chapters.");
                }
                if (currentAccessType == "FREE" && coinPrice > 0)
                {
                    throw new ArgumentException("Cannot set coin price for FREE chapters. Please change access type to PAID first.");
                }
                chapter.coin_price = currentAccessType == "FREE" ? 0 : coinPrice;
            }

            chapter.title = request.Title;
            // Chỉ cập nhật content khi client gửi field này; tránh PUT partial làm null → xóa nội dung.
            if (request.Content != null)
            {
                chapter.content = request.Content;
                chapter.word_count = CalculateWordCount(request.Content);
            }

            chapter.updated_at = DateTime.Now;

            if (request.OrderIndex.HasValue)
                chapter.order_index = request.OrderIndex.Value;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var newStatus = request.Status.ToUpper();
                var oldStatus = chapter.status?.ToUpper() ?? "DRAFT";

                if (newStatus == "PENDING_REVIEW")
                {
                    var updateStory = _storyLookup.GetById(chapter.story_id ?? Guid.Empty);
                    if (updateStory?.author_id is Guid updateAuthorId && _userLookup.IsAuthorWritingSuspended(updateAuthorId))
                        throw new InvalidOperationException("Tác giả đang bị tạm khóa chức năng viết truyện/chương (compliance/admin), không thể gửi xuất bản.");
                    EnsureCanSubmitForReview(chapter);
                }

                chapter.status = newStatus;

                if (newStatus == "PENDING_REVIEW" && oldStatus != "PENDING_REVIEW")
                    chapter.submitted_for_review_at = DateTime.UtcNow;
                else if (oldStatus == "PENDING_REVIEW" && newStatus != "PENDING_REVIEW")
                    chapter.submitted_for_review_at = null;

                // If changing to PUBLISHED, set published_at
                if (newStatus == "PUBLISHED" && oldStatus != "PUBLISHED")
                {
                    chapter.published_at = DateTime.Now;
                }
                // If changing from PUBLISHED to something else, clear published_at
                else if (oldStatus == "PUBLISHED" && newStatus != "PUBLISHED")
                {
                    chapter.published_at = null;
                }
            }

            if (request.AiContributionRatio.HasValue)
                chapter.ai_contribution_ratio = request.AiContributionRatio.Value;

            if (request.IsAiClean.HasValue)
                chapter.is_ai_clean = request.IsAiClean.Value;

            if (request.AiSimilarityPercent.HasValue)
                chapter.ai_similarity_percent = Math.Round(request.AiSimilarityPercent.Value, 2);

            _chapterRepository.Update(chapter);

            if (chapter.story_id.HasValue)
            {
                try
                {
                    UpdateStoryChapterStats(chapter.story_id.Value);

                    // If chapter status was changed to PUBLISHED, update story's last_published_at and notify followers
                    if (!string.IsNullOrWhiteSpace(request.Status) && request.Status.ToUpper() == "PUBLISHED")
                    {
                        var story = _storyLookup.GetById(chapter.story_id.Value);
                        if (story != null)
                        {
                            story.last_published_at = DateTime.Now;
                            _storyLookup.Update(story);
                        }
                        Console.WriteLine($"[CONSOLE] ChapterService.Update PUBLISHED -> NotifyStoryFollowersNewChapter StoryId={chapter.story_id} ChapterId={chapter.id}");
                        _logger.LogInformation("ChapterService.Update calling NotifyStoryFollowersNewChapter StoryId={StoryId} ChapterId={ChapterId}", chapter.story_id, chapter.id);
                        var createdNotifications = NotificationDAO.NotifyStoryFollowersNewChapter(chapter.story_id.Value, chapter.id, chapter.title, story?.title, _logger);
                        _ = PushNotificationsToFollowersAsync(createdNotifications);
                        if (story?.author_id != null)
                        {
                            var authorNotifications = NotificationDAO.NotifyAuthorFollowersNewChapter(story.author_id.Value, chapter.story_id.Value, chapter.id, chapter.title, story.title, _logger);
                            _ = PushNotificationsToFollowersAsync(authorNotifications);
                        }
                    }
                }
                catch (Exception)
                {
                    // Log error but don't fail the update operation
                    // The chapter was already updated successfully
                }
            }

            var finalStatus = chapter.status?.ToUpperInvariant() ?? "DRAFT";
            if (string.Equals(finalStatus, "PUBLISHED", StringComparison.OrdinalIgnoreCase)
                && chapter.story_id.HasValue
                && !string.IsNullOrWhiteSpace(chapter.content))
            {
                var contentUpdated = request.Content != null;
                var becamePublished = !string.Equals(previousStatus, "PUBLISHED", StringComparison.OrdinalIgnoreCase);
                if (contentUpdated || becamePublished)
                {
                    ChapterMemoryAnalysisScheduler.TrySchedule(
                        _scopeFactory,
                        _logger,
                        chapter.story_id.Value,
                        chapter.id,
                        chapter.title,
                        chapter.order_index,
                        chapter.content);
                }
            }

            return true;
        }

        public bool Delete(Guid id, bool deleteIncludingVersions = false)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;
            var storyForDelete = _storyLookup.GetById(chapter.story_id ?? Guid.Empty);
            EnsureStoryAuthorNotWritingSuspended(storyForDelete);
            EnsureStoryProgressAllowsChapterWrite(storyForDelete, "xóa chương");

            var statusUpper = (chapter.status ?? "").Trim().ToUpperInvariant();
            if (statusUpper != "DRAFT")
                throw new InvalidOperationException("Chỉ được xóa chương khi ở trạng thái Bản nháp. Chương hiện tại: " + (chapter.status ?? "—"));

            var versionCount = _versionRepository.GetByChapterId(id).Count();
            if (versionCount > 0 && !deleteIncludingVersions)
                ThrowRequiresVersionsDeleteConfirmation(versionCount);

            var storyId = chapter.story_id;

            try
            {
                ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, id);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Complete review assignment on chapter delete failed (non-fatal). ChapterId={ChapterId}", id);
            }

            _aiContentRepository.DeleteAllByChapterId(id);
            if (versionCount > 0)
                _versionRepository.DeleteAllByChapterId(id);

            _chapterRepository.Delete(id);

            if (storyId.HasValue)
            {
                try
                {
                    UpdateStoryChapterStats(storyId.Value);
                }
                catch (Exception)
                {
                    // Log error but don't fail the delete operation
                    // The chapter was already deleted successfully
                }
            }

            return true;
        }

        public bool Publish(Guid id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            var story = _storyLookup.GetById(chapter.story_id ?? Guid.Empty);
            if (story?.author_id is Guid authorId && _userLookup.IsAuthorWritingSuspended(authorId))
                throw new InvalidOperationException("Tác giả đang bị tạm khóa chức năng viết truyện/chương (compliance/admin), không thể gửi xuất bản.");
            EnsureStoryProgressAllowsChapterWrite(story, "gửi xuất bản chương");

            // Khi đã có phiên bản nào của chương đang chờ duyệt thì không cho gửi duyệt chapter gốc.
            var versionsPending = _versionRepository.GetByChapterId(id)
                .Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
            if (versionsPending)
                throw new InvalidOperationException("Chỉ được gửi một bản duyệt: bản gốc chương hoặc một phiên bản. Đã có phiên bản đang chờ duyệt.");

            EnsureCanSubmitForReview(chapter);

            // Author "Publish" = gửi chờ duyệt. Chỉ moderator approve mới chuyển sang PUBLISHED và set published_at.
            chapter.status = "PENDING_REVIEW";
            chapter.updated_at = DateTime.Now;
            chapter.submitted_for_review_at = DateTime.UtcNow;
            // published_at và story.last_published_at chỉ set khi moderator approve (ModerationService.ApproveChapter)

            _chapterRepository.Update(chapter);
            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        public bool Unpublish(Guid id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            var storyUnpublish = _storyLookup.GetById(chapter.story_id ?? Guid.Empty);
            EnsureStoryAuthorNotWritingSuspended(storyUnpublish);

            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeChapter, id))
                throw new InvalidOperationException("Kiểm duyệt viên đã nhận duyệt đơn này, bạn không thể hủy xuất bản. Vui lòng chờ kết quả duyệt.");

            EnsureCanUnpublish(chapter);

            chapter.status = "DRAFT";
            chapter.updated_at = DateTime.Now;
            chapter.submitted_for_review_at = null;

            _chapterRepository.Update(chapter);

            // Giải phóng đơn đã nhận (claim) của moderator khi tác giả hủy xuất bản — để chương có thể hiển thị lại trong "Nhận duyệt đơn" khi tác giả gửi xuất bản lại.
            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, id);

            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        public bool Reorder(Guid id, int newOrderIndex)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            var storyReorder = _storyLookup.GetById(chapter.story_id ?? Guid.Empty);
            EnsureStoryAuthorNotWritingSuspended(storyReorder);

            var storyId = chapter.story_id ?? Guid.Empty;
            var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, newOrderIndex);
            if (existingChapter != null && existingChapter.id != id)
            {
                var tempOrder = chapter.order_index;
                chapter.order_index = newOrderIndex;
                existingChapter.order_index = tempOrder;

                _chapterRepository.Update(chapter);
                _chapterRepository.Update(existingChapter);
            }
            else
            {
                chapter.order_index = newOrderIndex;
                _chapterRepository.Update(chapter);
            }

            return true;
        }

        private static void ThrowRequiresVersionsDeleteConfirmation(int versionCount)
        {
            var ex = new InvalidOperationException(
                "Chương có phiên bản (nháp / chỉnh sửa) đã lưu. Vui lòng xác nhận trên giao diện: nếu đồng ý, hệ thống sẽ xóa luôn toàn bộ phiên bản và chương.");
            ex.Data["ErrorCode"] = DeleteRequiresVersionsConfirmationCode;
            ex.Data["VersionCount"] = versionCount;
            throw ex;
        }

        /// <summary>Tác giả chỉ được gửi xuất bản chương theo thứ tự 1, 2, 3... Chương trước phải đã gửi (PUBLISHED, PENDING_REVIEW, hoặc có ít nhất một version PENDING_REVIEW) thì mới gửi được chương tiếp theo.</summary>
        private void EnsureCanSubmitForReview(chapters chapter)
        {
            if (chapter.order_index <= 0)
                return;
            var storyId = chapter.story_id ?? Guid.Empty;
            var previous = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, chapter.order_index - 1);
            if (previous == null)
            {
                throw new InvalidOperationException(
                    "Phải gửi xuất bản chương theo thứ tự. Chương " + (chapter.order_index) + " chưa được gửi hoặc chưa duyệt, không thể gửi chương " + (chapter.order_index + 1) + ".");
            }
            var prevStatus = (previous.status ?? "").Trim().ToUpperInvariant();
            var prevHasPendingVersion = _versionRepository.GetByChapterId(previous.id).Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
            if (prevStatus != "PUBLISHED" && prevStatus != "PENDING_REVIEW" && !prevHasPendingVersion)
            {
                throw new InvalidOperationException(
                    "Phải gửi xuất bản chương theo thứ tự. Chương " + (chapter.order_index) + " chưa được gửi hoặc chưa duyệt, không thể gửi chương " + (chapter.order_index + 1) + ".");
            }
        }

        private void EnsureStoryAuthorNotWritingSuspended(stories? story)
        {
            if (story?.author_id is Guid aid && _userLookup.IsAuthorWritingSuspended(aid))
                throw new InvalidOperationException("Tác giả đang bị tạm khóa chức năng viết truyện/chương (compliance/admin).");
        }

        private static void EnsureStoryProgressAllowsChapterWrite(stories? story, string actionVi)
        {
            var progress = (story?.story_progress_status ?? "ONGOING").Trim().ToUpperInvariant();
            if (progress == "HIATUS" || progress == "COMPLETED")
                throw new InvalidOperationException($"Truyện đang ở trạng thái {(progress == "COMPLETED" ? "Hoàn thành" : "Tạm dừng")}, không thể {actionVi}.");
        }

        /// <summary>Hủy xuất bản phải theo thứ tự ngược: chỉ được hủy chương N nếu không còn chương nào có thứ tự > N đang xuất bản hoặc chờ duyệt.</summary>
        private void EnsureCanUnpublish(chapters chapter)
        {
            var storyId = chapter.story_id ?? Guid.Empty;
            if (storyId == Guid.Empty) return;
            var allChapters = _chapterRepository.GetByStoryId(storyId).OrderBy(c => c.order_index).ToList();
            var currentIndex = chapter.order_index;
            foreach (var c in allChapters)
            {
                if (c.order_index <= currentIndex) continue;
                var status = (c.status ?? "").Trim().ToUpperInvariant();
                if (status == "PUBLISHED" || status == "PENDING_REVIEW")
                    throw new InvalidOperationException("Hủy xuất bản phải theo thứ tự ngược. Phải hủy chương " + (c.order_index + 1) + " trước rồi mới hủy chương " + (currentIndex + 1) + ".");
                var hasPendingVersion = _versionRepository.GetByChapterId(c.id).Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
                if (hasPendingVersion)
                    throw new InvalidOperationException("Hủy xuất bản phải theo thứ tự ngược. Chương " + (c.order_index + 1) + " đang có phiên bản chờ duyệt, phải xử lý trước rồi mới hủy chương " + (currentIndex + 1) + ".");
            }
        }

        /// <summary>Gửi real-time (SignalR) từng thông báo tới user theo dõi truyện. Gọi fire-and-forget từ Create/Update.</summary>
        private async Task PushNotificationsToFollowersAsync(List<notifications> created)
        {
            if (created == null || created.Count == 0 || _notificationHubNotifier == null)
                return;
            foreach (var n in created)
            {
                if (n.user_id == null) continue;
                try
                {
                    var dto = new NotificationDto
                    {
                        Id = n.id,
                        Type = n.type,
                        Title = n.title,
                        Content = n.content,
                        LinkUrl = n.link_url,
                        IsRead = n.is_read == true,
                        CreatedAt = n.created_at
                    };
                    await _notificationHubNotifier.NotifyUserAsync(n.user_id.Value, dto);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Push notification to follower failed. UserId={UserId} NotificationId={NotificationId}", n.user_id, n.id);
                }
            }
        }

        private int CalculateWordCount(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            return content
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }

        private void EnsureUniqueChapterTitleForStory(Guid storyId, string? title, Guid? excludeChapterId)
        {
            var normalizedTitle = NormalizeTitle(title);
            if (string.IsNullOrWhiteSpace(normalizedTitle))
                return;

            var duplicated = _chapterRepository
                .GetByStoryId(storyId)
                .Any(c => (!excludeChapterId.HasValue || c.id != excludeChapterId.Value) &&
                          NormalizeTitle(c.title) == normalizedTitle);
            if (duplicated)
                throw new InvalidOperationException("Tên chương đã tồn tại trong truyện này. Vui lòng đặt tên khác.");
        }

        private static string NormalizeTitle(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;
            return string.Join(" ", title.Trim().Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
                .ToLowerInvariant();
        }

        private void UpdateStoryChapterStats(Guid storyId)
        {
            var story = _storyLookup.GetById(storyId);
            if (story == null)
                return;

            var chapterList = _chapterRepository.GetByStoryId(storyId).ToList();

            story.total_chapters = chapterList.Count;
            story.word_count = chapterList.Sum(c => c.word_count ?? 0);
            story.updated_at = DateTime.Now;

            _storyLookup.Update(story);
        }

        private ChapterResponseDto MapToResponseDto(chapters chapter, bool includeStoryLookup = true, string? storyTitleOverride = null)
        {
            string? storyTitle = storyTitleOverride;
            if (storyTitle == null && includeStoryLookup && chapter.story_id.HasValue)
                storyTitle = _storyLookup.GetById(chapter.story_id.Value)?.title;

            return new ChapterResponseDto
            {
                Id = chapter.id,
                StoryId = chapter.story_id,
                StoryTitle = storyTitle,
                Title = chapter.title,
                OrderIndex = chapter.order_index,
                Content = chapter.content,
                Status = chapter.status,
                AccessType = chapter.access_type,
                CoinPrice = chapter.coin_price,
                WordCount = chapter.word_count,
                AiContributionRatio = chapter.ai_contribution_ratio,
                AiSimilarityPercent = chapter.ai_similarity_percent,
                IsAiClean = chapter.is_ai_clean ?? false,
                PublishedAt = chapter.published_at,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at
            };
        }

        private static void ApplyChapterCommentCounts(List<ChapterListItemDto> items, IEnumerable<Guid> chapterIds)
        {
            var countMap = CommentDAO.GetApprovedCommentCountsByChapterIds(chapterIds);
            foreach (var dto in items)
                dto.CommentCount = countMap.GetValueOrDefault(dto.Id, 0);
        }

        private ChapterListItemDto MapToListItemDto(chapters chapter, string? storyTitle = null)
        {
            return new ChapterListItemDto
            {
                Id = chapter.id,
                StoryId = chapter.story_id,
                StoryTitle = storyTitle,
                Title = chapter.title,
                OrderIndex = chapter.order_index,
                Status = chapter.status,
                AccessType = chapter.access_type,
                CoinPrice = chapter.coin_price,
                WordCount = chapter.word_count,
                AiSimilarityPercent = chapter.ai_similarity_percent,
                AiContributionRatio = chapter.ai_contribution_ratio,
                PublishedAt = chapter.published_at,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at
            };
        }

        /// <summary>
        /// Điền PendingSince, DeadlineAt (SLA), TimeStatus, ClaimedAt, ClaimedByDisplayName cho list API
        /// (cùng quy tắc queue moderator; tránh gán mốc sai cho chương không liên quan duyệt).
        /// </summary>
        private static void EnrichChapterListItemsWithReviewSla(IReadOnlyList<chapters> chapterEntities, List<ChapterListItemDto> items)
        {
            if (chapterEntities.Count == 0 || items.Count != chapterEntities.Count)
                return;

            var ids = chapterEntities.Select(c => c.id).ToList();
            var claims = ReviewAssignmentDAO.GetActiveClaimInfosByTargetIds(ReviewAssignmentDAO.TargetTypeChapter, ids);
            var pendingVersionMaxCreated = ChapterVersionDAO.GetMaxPendingReviewCreatedAtByChapterIds(ids);

            for (var i = 0; i < items.Count; i++)
            {
                var c = chapterEntities[i];
                var dto = items[i];
                var hasPendingVersion = pendingVersionMaxCreated.ContainsKey(c.id);
                var hasClaim = claims.ContainsKey(c.id);
                var pendingReviewStatus = string.Equals(c.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase);
                if (!pendingReviewStatus && !hasPendingVersion && !hasClaim)
                    continue;

                DateTime? authorSubmitted = null;
                if (c.submitted_for_review_at.HasValue)
                    authorSubmitted = ModeratorReviewSlaHelper.NormalizeToUtc(c.submitted_for_review_at.Value);
                else
                {
                    var rawBase = c.updated_at ?? c.created_at;
                    var baseUtc = rawBase.HasValue ? ModeratorReviewSlaHelper.NormalizeToUtc(rawBase.Value) : (DateTime?)null;
                    if (pendingVersionMaxCreated.TryGetValue(c.id, out var maxPendingRaw))
                    {
                        var maxPending = ModeratorReviewSlaHelper.NormalizeToUtc(maxPendingRaw);
                        if (baseUtc.HasValue)
                            authorSubmitted = maxPending > baseUtc.Value ? maxPending : baseUtc.Value;
                        else
                            authorSubmitted = maxPending;
                    }
                    else
                        authorSubmitted = baseUtc;
                }

                dto.PendingSince = authorSubmitted;

                (Guid AssigneeId, DateTime AssignedAt, string DisplayName, DateTime? ReviewDeadlineAt)? claim = null;
                if (claims.TryGetValue(c.id, out var tuple))
                    claim = tuple;

                if (claim.HasValue)
                {
                    dto.ClaimedAt = claim.Value.AssignedAt;
                    dto.ClaimedByDisplayName = claim.Value.DisplayName;
                }

                var fallbackDeadline = ResolveChapterListReviewDeadlineUtc(authorSubmitted, claim);
                dto.DeadlineAt = fallbackDeadline;
                dto.TimeStatus = ModeratorReviewSlaHelper.ComputeSlaTimeStatus(authorSubmitted, fallbackDeadline);
            }
        }

        /// <summary>Gắn toàn bộ lần từ chối chương gốc từ moderation_logs (không chỉ lần mới nhất).</summary>
        private static void EnrichModeratorRejectionHistoryForChapterList(IReadOnlyList<chapters> chapterEntities, List<ChapterListItemDto> items)
        {
            if (chapterEntities.Count == 0 || items.Count != chapterEntities.Count)
                return;

            var ids = chapterEntities.Select(c => c.id).ToList();
            var map = ModerationLogDAO.GetRejectionHistoriesByTargetIds(ReviewAssignmentDAO.TargetTypeChapter, ids);
            for (var i = 0; i < items.Count; i++)
            {
                var chapterId = items[i].Id;
                if (!map.TryGetValue(chapterId, out var tuples) || tuples.Count == 0)
                    continue;
                items[i].ModeratorRejectionHistory = tuples.Select(t => new ChapterRejectionHistoryItemDto
                {
                    Reason = t.Reason,
                    RejectedAt = t.RejectedAt,
                    ModeratorId = t.ModeratorId
                }).ToList();
            }
        }

        /// <summary>Đã nhận duyệt: ưu tiên hạn moderator; bản cũ: assigned_at + 7 ngày. Chưa nhận: mốc gửi + 7 ngày.</summary>
        private static DateTime? ResolveChapterListReviewDeadlineUtc(
            DateTime? pendingSince,
            (Guid AssigneeId, DateTime AssignedAt, string DisplayName, DateTime? ReviewDeadlineAt)? claim)
        {
            if (claim.HasValue)
            {
                if (claim.Value.ReviewDeadlineAt.HasValue)
                    return ModeratorReviewSlaHelper.NormalizeToUtc(claim.Value.ReviewDeadlineAt.Value);
                return claim.Value.AssignedAt.AddDays(ModeratorReviewSlaHelper.PolicyDaysAfterAuthorSubmit);
            }

            if (pendingSince.HasValue)
                return pendingSince.Value.AddDays(ModeratorReviewSlaHelper.PolicyDaysAfterAuthorSubmit);
            return null;
        }
    }
}