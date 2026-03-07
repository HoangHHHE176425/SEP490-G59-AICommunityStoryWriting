using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Repositories;
using Services.DTOs.Stories;
using Services.Interfaces;

namespace Services.Implementations
{
    public class StoryService : IStoryService
    {
        private const string ViewCacheKeyPrefix = "story_view:";
        private static readonly TimeSpan ViewCooldown = TimeSpan.FromHours(24);

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly ILogger<StoryService> _logger;
        private readonly IModerationHubNotifier? _moderationHubNotifier;
        private readonly IMemoryCache _cache;

        public StoryService(IStoryRepository storyRepository, IChapterRepository chapterRepository, ILogger<StoryService> logger, IMemoryCache cache, IModerationHubNotifier? moderationHubNotifier = null)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _logger = logger;
            _cache = cache;
            _moderationHubNotifier = moderationHubNotifier;
        }

        public StoryResponseDto Create(CreateStoryRequestDto request, Guid authorId, string? coverImageUrl)
        {
            if (!UserDAO.Exists(authorId))
            {
                throw new InvalidOperationException(
                    "AuthorId không tồn tại trong bảng users. Vui lòng kiểm tra DefaultAuthorIdForStories trong appsettings.json (dùng Guid của user có trong bảng users).");
            }

            if (request.CategoryIds == null || !request.CategoryIds.Any())
            {
                throw new InvalidOperationException("Chọn ít nhất một thể loại.");
            }

            foreach (var categoryId in request.CategoryIds)
            {
                var category = CategoryDAO.GetById(categoryId);
                if (category == null)
                    throw new InvalidOperationException($"Category with ID {categoryId} not found.");
                if (!category.is_active ?? false)
                    throw new InvalidOperationException($"Category '{category.name}' is not active.");
            }

            var slug = GenerateSlug(request.Title);
            var existingBySlug = _storyRepository.GetBySlug(slug);
            if (existingBySlug != null)
            {
                throw new InvalidOperationException($"Story with slug '{slug}' already exists.");
            }

            var validAgeRatings = new[] { "ALL", "13+", "16+", "18+" };
            if (!validAgeRatings.Contains(request.AgeRating?.ToUpper()))
            {
                throw new ArgumentException($"Invalid age rating. Must be one of: {string.Join(", ", validAgeRatings)}");
            }

            var validProgressStatuses = new[] { "ONGOING", "COMPLETED", "HIATUS" };
            var progressStatus = (request.StoryProgressStatus ?? "ONGOING").ToUpper();
            if (!validProgressStatuses.Contains(progressStatus))
            {
                throw new ArgumentException($"Invalid story progress status. Must be one of: {string.Join(", ", validProgressStatuses)}");
            }

            var story = new stories
            {
                id = Guid.NewGuid(),
                title = request.Title,
                slug = slug,
                summary = request.Summary,
                author_id = authorId,
                cover_image = coverImageUrl,
                status = "DRAFT",
                story_progress_status = progressStatus,
                age_rating = (request.AgeRating ?? "ALL").ToUpper(),
                total_chapters = 0,
                total_views = 0,
                total_favorites = 0,
                avg_rating = 0,
                word_count = 0,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            _storyRepository.Add(story, request.CategoryIds);

            var createdStory = _storyRepository.GetById(story.id);
            return MapToResponseDto(createdStory!);
        }

        public PagedResultDto<StoryListItemDto> GetAll(StoryQueryDto query)
        {
            var storiesQuery = _storyRepository.GetAll();

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                storiesQuery = storiesQuery.Where(s =>
                    s.title.ToLower().Contains(searchLower) ||
                    (s.summary != null && s.summary.ToLower().Contains(searchLower)));
            }

            if (query.CategoryId.HasValue)
            {
                storiesQuery = storiesQuery.Where(s => s.category.Any(c => c.id == query.CategoryId.Value));
            }

            if (query.CategoryIds != null && query.CategoryIds.Count > 0)
            {
                var ids = query.CategoryIds;
                storiesQuery = storiesQuery.Where(s => s.category.Any(c => ids.Contains(c.id)));
            }

            if (query.ExcludeStoryIds != null && query.ExcludeStoryIds.Count > 0)
            {
                var excludeIds = query.ExcludeStoryIds;
                storiesQuery = storiesQuery.Where(s => !excludeIds.Contains(s.id));
            }

            if (query.IncludeStoryIds != null && query.IncludeStoryIds.Count > 0)
            {
                var includeIds = query.IncludeStoryIds;
                storiesQuery = storiesQuery.Where(s => includeIds.Contains(s.id));
            }

            if (query.AuthorId.HasValue)
            {
                storiesQuery = storiesQuery.Where(s => s.author_id == query.AuthorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                storiesQuery = storiesQuery.Where(s => s.status == query.Status);
            }

            storiesQuery = query.SortBy?.ToLower() switch
            {
                "updated_at" => query.SortOrder == "asc"
                    ? storiesQuery.OrderBy(s => s.updated_at)
                    : storiesQuery.OrderByDescending(s => s.updated_at),
                "total_views" => query.SortOrder == "asc"
                    ? storiesQuery.OrderBy(s => s.total_views)
                    : storiesQuery.OrderByDescending(s => s.total_views),
                "avg_rating" => query.SortOrder == "asc"
                    ? storiesQuery.OrderBy(s => s.avg_rating)
                    : storiesQuery.OrderByDescending(s => s.avg_rating),
                _ => query.SortOrder == "asc"
                    ? storiesQuery.OrderBy(s => s.created_at)
                    : storiesQuery.OrderByDescending(s => s.created_at)
            };

            var totalCount = storiesQuery.Count();

            var stories = storiesQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new PagedResultDto<StoryListItemDto>
            {
                Items = stories.Select(MapToListItemDto),
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public StoryResponseDto? GetById(Guid id)
        {
            var story = _storyRepository.GetById(id);
            if (story == null) return null;
            var dto = MapToResponseDto(story);
            if (story.status == "REJECTED")
            {
                var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("STORY", id);
                dto.RejectionReason = reason;
                dto.RejectedAt = rejectedAt;
            }
            return dto;
        }

        public StoryResponseDto? GetBySlug(string slug)
        {
            var story = _storyRepository.GetBySlug(slug);
            if (story == null) return null;
            var dto = MapToResponseDto(story);
            if (story.status == "REJECTED")
            {
                var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("STORY", story.id);
                dto.RejectionReason = reason;
                dto.RejectedAt = rejectedAt;
            }
            return dto;
        }

        public PagedResultDto<StoryListItemDto> GetByAuthor(Guid authorId, StoryQueryDto query)
        {
            var authorQuery = new StoryQueryDto
            {
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search,
                CategoryId = query.CategoryId,
                AuthorId = authorId,
                Status = query.Status,
                SortBy = query.SortBy,
                SortOrder = query.SortOrder
            };

            return GetAll(authorQuery);
        }

        public bool Update(Guid id, UpdateStoryRequestDto request)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            // Lưu version (vết tích) trước khi sửa nếu story đã public
            if (string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
            {
                StoryVersionDAO.SaveVersion(story, request.ChangeSummary);
            }

            if (request.CategoryIds != null && request.CategoryIds.Any())
            {
                foreach (var categoryId in request.CategoryIds)
                {
                    var category = CategoryDAO.GetById(categoryId);
                    if (category == null)
                        throw new InvalidOperationException($"Category with ID {categoryId} not found.");
                    if (!category.is_active ?? false)
                        throw new InvalidOperationException($"Category '{category.name}' is not active.");
                }
            }

            if (request.Title != story.title)
            {
                var newSlug = GenerateSlug(request.Title);
                if (newSlug != story.slug)
                {
                    var existingStory = _storyRepository.GetBySlug(newSlug);
                    if (existingStory != null && existingStory.id != id)
                    {
                        throw new InvalidOperationException($"Story with slug '{newSlug}' already exists.");
                    }
                    story.slug = newSlug;
                }
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var validStatuses = new[] { "DRAFT", "PENDING_REVIEW", "REJECTED", "PUBLISHED", "HIDDEN", "COMPLETED", "CANCELLED" };
                if (!validStatuses.Contains(request.Status.ToUpper()))
                {
                    throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                }
            }

            if (!string.IsNullOrWhiteSpace(request.AgeRating))
            {
                var validAgeRatings = new[] { "ALL", "13+", "16+", "18+" };
                if (!validAgeRatings.Contains(request.AgeRating.ToUpper()))
                {
                    throw new ArgumentException($"Invalid age rating. Must be one of: {string.Join(", ", validAgeRatings)}");
                }
            }

            if (!string.IsNullOrWhiteSpace(request.StoryProgressStatus))
            {
                var validProgressStatuses = new[] { "ONGOING", "COMPLETED", "HIATUS" };
                if (!validProgressStatuses.Contains(request.StoryProgressStatus.ToUpper()))
                {
                    throw new ArgumentException($"Invalid story progress status. Must be one of: {string.Join(", ", validProgressStatuses)}");
                }
            }

            story.title = request.Title;
            story.summary = request.Summary;
            story.updated_at = DateTime.Now;

            if (!string.IsNullOrWhiteSpace(request.CoverImageUrl))
            {
                story.cover_image = request.CoverImageUrl;
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
                story.status = request.Status.ToUpper();

            if (!string.IsNullOrWhiteSpace(request.StoryProgressStatus))
                story.story_progress_status = request.StoryProgressStatus.ToUpper();

            if (!string.IsNullOrWhiteSpace(request.AgeRating))
                story.age_rating = request.AgeRating.ToUpper();

            story.category.Clear();
            _storyRepository.Update(story);
            if (request.CategoryIds != null)
                StoryDAO.UpdateStoryCategories(id, request.CategoryIds);
            return true;
        }

        public bool Delete(Guid id)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            // Delete all associated chapters first
            try
            {
                _chapterRepository.DeleteByStoryId(id);
            }
            catch (Exception)
            {
                // Log error but continue with story deletion
                // If chapters fail to delete, database constraints will prevent story deletion
            }

            _storyRepository.Delete(id);
            return true;
        }

        public bool Publish(Guid id)
        {
            try
            {
                _logger?.LogInformation("StoryService.Publish: Starting publish for story ID: {StoryId}", id);

                var story = _storyRepository.GetById(id);
                if (story == null)
                {
                    _logger?.LogWarning("StoryService.Publish: Story with ID {StoryId} not found", id);
                    return false;
                }

                _logger?.LogInformation("StoryService.Publish: Found story '{Title}' (ID: {StoryId}), current status: {Status}",
                    story.title, id, story.status);

                // Author "Publish" = gửi chờ duyệt. Chỉ moderator approve mới chuyển sang PUBLISHED.
                story.status = "PENDING_REVIEW";
                story.updated_at = DateTime.Now;
                // published_at, last_published_at chỉ set khi moderator approve (ModerationService.ApproveStory)

                _logger?.LogInformation("StoryService.Publish: Updating story status to PENDING_REVIEW for ID: {StoryId}", id);
                _storyRepository.Update(story);

                _logger?.LogInformation("StoryService.Publish: Successfully submitted story for review (PENDING_REVIEW) ID: {StoryId}", id);
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "StoryService.Publish: Error publishing story ID: {StoryId}. Error: {ErrorMessage}",
                    id, ex.Message);

                if (ex.InnerException != null)
                {
                    _logger?.LogError("StoryService.Publish: Inner exception: {InnerException}", ex.InnerException.Message);
                }

                throw; // Re-throw to be handled by controller
            }
        }

        public bool Unpublish(Guid id)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            story.status = "DRAFT";
            story.updated_at = DateTime.Now;

            _storyRepository.Update(story);
            return true;
        }

        public void RecordViewIfAllowed(Guid storyId, string viewerKey)
        {
            if (string.IsNullOrWhiteSpace(viewerKey))
                return;
            var cacheKey = $"{ViewCacheKeyPrefix}{storyId}:{viewerKey}";
            if (_cache.TryGetValue(cacheKey, out _))
                return;
            var story = _storyRepository.GetById(storyId);
            if (story == null || story.status != "PUBLISHED")
                return;
            _cache.Set(cacheKey, true, ViewCooldown);
            _storyRepository.IncrementViewCount(storyId);
        }

        public void RecordReadStory(Guid storyId, Guid userId, string? ipAddress = null, string? deviceInfo = null)
        {
            if (userId == Guid.Empty || storyId == Guid.Empty)
                return;
            try
            {
                UserActivityLogDAO.LogReadStory(userId, storyId, ipAddress, deviceInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log read story activity. storyId={StoryId}, userId={UserId}", storyId, userId);
            }
        }

        public void RecordReadChapter(Guid storyId, Guid chapterId, Guid userId, string? ipAddress = null, string? deviceInfo = null)
        {
            if (userId == Guid.Empty || storyId == Guid.Empty || chapterId == Guid.Empty)
                return;
            try
            {
                UserActivityLogDAO.LogReadChapter(userId, storyId, chapterId, ipAddress, deviceInfo);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to log read chapter activity. storyId={StoryId}, chapterId={ChapterId}, userId={UserId}", storyId, chapterId, userId);
            }
        }

        public (decimal avgRating, int ratingCount) RateStory(Guid storyId, Guid userId, int starValue, string? reviewText)
        {
            if (storyId == Guid.Empty)
                throw new InvalidOperationException("StoryId không hợp lệ.");
            if (userId == Guid.Empty)
                throw new InvalidOperationException("UserId không hợp lệ.");
            if (starValue < 1 || starValue > 5)
                throw new InvalidOperationException("StarValue phải từ 1 đến 5.");

            var story = _storyRepository.GetById(storyId);
            if (story == null)
                throw new InvalidOperationException("Truyện không tồn tại.");
            if (!string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Chỉ có thể đánh giá truyện đã được phát hành (PUBLISHED).");

            // Chặn rating khi chưa đọc chapter: yêu cầu có log READ_CHAPTER cho story.
            if (!UserActivityLogDAO.HasReadAnyChapterOfStory(userId, storyId))
                throw new InvalidOperationException("Bạn cần đọc ít nhất một chapter trước khi đánh giá.");

            RatingDAO.Upsert(userId, storyId, starValue, reviewText, status: "VISIBLE");
            var (avg, count) = RatingDAO.GetAverageAndCount(storyId, status: "VISIBLE");
            StoryDAO.UpdateAvgRating(storyId, avg);
            return (avg, count);
        }

        public (string? reason, DateTime? rejectedAt) GetLatestRejectionForStory(Guid storyId)
        {
            return ModerationLogDAO.GetLatestRejection("STORY", storyId);
        }

        private string GenerateSlug(string title)
        {
            return title
                .ToLower()
                .Trim()
                .Replace(" ", "-")
                .Replace("đ", "d")
                .Replace("Đ", "d")
                .Replace("á", "a")
                .Replace("à", "a")
                .Replace("ả", "a")
                .Replace("ã", "a")
                .Replace("ạ", "a")
                .Replace("ă", "a")
                .Replace("ắ", "a")
                .Replace("ằ", "a")
                .Replace("ẳ", "a")
                .Replace("ẵ", "a")
                .Replace("ặ", "a")
                .Replace("â", "a")
                .Replace("ấ", "a")
                .Replace("ầ", "a")
                .Replace("ẩ", "a")
                .Replace("ẫ", "a")
                .Replace("ậ", "a")
                .Replace("é", "e")
                .Replace("è", "e")
                .Replace("ẻ", "e")
                .Replace("ẽ", "e")
                .Replace("ẹ", "e")
                .Replace("ê", "e")
                .Replace("ế", "e")
                .Replace("ề", "e")
                .Replace("ể", "e")
                .Replace("ễ", "e")
                .Replace("ệ", "e")
                .Replace("í", "i")
                .Replace("ì", "i")
                .Replace("ỉ", "i")
                .Replace("ĩ", "i")
                .Replace("ị", "i")
                .Replace("ó", "o")
                .Replace("ò", "o")
                .Replace("ỏ", "o")
                .Replace("õ", "o")
                .Replace("ọ", "o")
                .Replace("ô", "o")
                .Replace("ố", "o")
                .Replace("ồ", "o")
                .Replace("ổ", "o")
                .Replace("ỗ", "o")
                .Replace("ộ", "o")
                .Replace("ơ", "o")
                .Replace("ớ", "o")
                .Replace("ờ", "o")
                .Replace("ở", "o")
                .Replace("ỡ", "o")
                .Replace("ợ", "o")
                .Replace("ú", "u")
                .Replace("ù", "u")
                .Replace("ủ", "u")
                .Replace("ũ", "u")
                .Replace("ụ", "u")
                .Replace("ư", "u")
                .Replace("ứ", "u")
                .Replace("ừ", "u")
                .Replace("ử", "u")
                .Replace("ữ", "u")
                .Replace("ự", "u")
                .Replace("ý", "y")
                .Replace("ỳ", "y")
                .Replace("ỷ", "y")
                .Replace("ỹ", "y")
                .Replace("ỵ", "y");
        }

        private StoryResponseDto MapToResponseDto(stories story)
        {
            var categories = story.category?.ToList() ?? new List<categories>();
            var categoryIds = categories.Select(c => c.id).ToList();
            var categoryNames = categories.Any() ? string.Join(", ", categories.Select(c => c.name)) : null;

            var totalChapters = story.total_chapters ?? ChapterDAO.GetCountByStoryId(story.id);
            var publishedChaptersCount = ChapterDAO.GetPublishedCountByStoryId(story.id);
            var totalComments = CommentDAO.GetCountByStoryId(story.id);
            var latestChapterUpdatedAt = ChapterDAO.GetLatestUpdatedAtByStoryId(story.id);
            var latestUpdatedAt = story.updated_at;
            if (latestChapterUpdatedAt.HasValue && (!latestUpdatedAt.HasValue || latestChapterUpdatedAt > latestUpdatedAt))
                latestUpdatedAt = latestChapterUpdatedAt;

            return new StoryResponseDto
            {
                Id = story.id,
                Title = story.title,
                Slug = story.slug,
                Summary = story.summary,
                CategoryIds = categoryIds,
                CategoryNames = categoryNames,
                AuthorId = story.author_id,
                CoverImage = story.cover_image,
                Status = story.status,
                StoryProgressStatus = story.story_progress_status,
                AgeRating = story.age_rating,
                TotalChapters = totalChapters,
                PublishedChaptersCount = publishedChaptersCount,
                TotalViews = story.total_views,
                TotalComments = totalComments,
                TotalFavorites = story.total_favorites,
                AvgRating = story.avg_rating,
                WordCount = story.word_count,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                LatestUpdatedAt = latestUpdatedAt,
                PublishedAt = story.published_at,
                LastPublishedAt = story.last_published_at
            };
        }

        private StoryListItemDto MapToListItemDto(stories story)
        {
            var categories = story.category?.ToList() ?? new List<categories>();
            var categoryIds = categories.Select(c => c.id).ToList();
            var categoryNames = categories.Any() ? string.Join(", ", categories.Select(c => c.name)) : null;

            var totalChapters = story.total_chapters ?? ChapterDAO.GetCountByStoryId(story.id);
            var publishedChaptersCount = ChapterDAO.GetPublishedCountByStoryId(story.id);
            var totalComments = CommentDAO.GetCountByStoryId(story.id);
            var latestChapterUpdatedAt = ChapterDAO.GetLatestUpdatedAtByStoryId(story.id);
            var latestUpdatedAt = story.updated_at;
            if (latestChapterUpdatedAt.HasValue && (!latestUpdatedAt.HasValue || latestChapterUpdatedAt > latestUpdatedAt))
                latestUpdatedAt = latestChapterUpdatedAt;

            return new StoryListItemDto
            {
                Id = story.id,
                Title = story.title,
                Slug = story.slug,
                Summary = story.summary,
                Status = story.status,
                StoryProgressStatus = story.story_progress_status,
                CoverImage = story.cover_image,
                CategoryIds = categoryIds,
                CategoryNames = categoryNames,
                AuthorId = story.author_id,
                TotalChapters = totalChapters,
                PublishedChaptersCount = publishedChaptersCount,
                TotalViews = story.total_views,
                TotalComments = totalComments,
                TotalFavorites = story.total_favorites,
                AvgRating = story.avg_rating,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                LatestUpdatedAt = latestUpdatedAt
            };
        }
    }
}