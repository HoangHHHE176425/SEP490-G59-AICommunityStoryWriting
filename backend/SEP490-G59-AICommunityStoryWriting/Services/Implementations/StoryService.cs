using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Repositories;
using Services.DTOs.Community;
using Services.DTOs.Stories;
using Services.Interfaces;
using System.Globalization;
using System.Text;

namespace Services.Implementations
{
    public class StoryService : IStoryService
    {
        private const string ViewCacheKeyPrefix = "story_view:";
        private static readonly TimeSpan ViewCooldown = TimeSpan.FromHours(24);
        /// <summary>Giới hạn cột <c>stories.slug</c> trong DB; chừa chỗ cho hậu tố <c>-2</c>, <c>-3</c>, … khi trùng base.</summary>
        private const int StorySlugMaxLength = 255;
        /// <summary>Giới hạn tiêu đề khi tạo truyện (ma trận nghiệp vụ / UI; cột DB cho phép 255).</summary>
        private const int StoryTitleMaxBusinessLength = 50;
        /// <summary>Giới hạn nội dung mô tả khi tạo/cập nhật (cột summary thường nvarchar(max); giới hạn API để kiểm soát kích thước).</summary>
        private const int StorySummaryMaxLength = 4000;

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IUserLookup _userLookup;
        private readonly ICategoryLookup _categoryLookup;
        private readonly ILogger<StoryService> _logger;
        private readonly IModerationHubNotifier? _moderationHubNotifier;
        private readonly IMemoryCache _cache;

        public StoryService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IUserLookup userLookup,
            ICategoryLookup categoryLookup,
            ILogger<StoryService> logger,
            IMemoryCache cache,
            IModerationHubNotifier? moderationHubNotifier = null)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _userLookup = userLookup;
            _categoryLookup = categoryLookup;
            _logger = logger;
            _cache = cache;
            _moderationHubNotifier = moderationHubNotifier;
        }

        public StoryResponseDto Create(CreateStoryRequestDto request, Guid authorId, string? coverImageUrl)
        {
            if (!_userLookup.Exists(authorId))
            {
                throw new InvalidOperationException(
                    "AuthorId không tồn tại trong bảng users. Vui lòng kiểm tra DefaultAuthorIdForStories trong appsettings.json (dùng Guid của user có trong bảng users).");
            }

            if (_userLookup.IsAuthorWritingSuspended(authorId))
                throw new InvalidOperationException("Tài khoản đang bị tạm khóa chức năng viết truyện (compliance/admin).");

            if (request.CategoryIds == null || !request.CategoryIds.Any())
            {
                throw new InvalidOperationException("Chọn ít nhất một thể loại.");
            }

            foreach (var categoryId in request.CategoryIds)
            {
                var category = _categoryLookup.GetById(categoryId);
                if (category == null)
                    throw new InvalidOperationException($"Category with ID {categoryId} not found.");
                if (!category.is_active ?? false)
                    throw new InvalidOperationException($"Category '{category.name}' is not active.");
            }

            if (string.IsNullOrWhiteSpace(request.Title))
                throw new InvalidOperationException("Vui lòng điền đầy đủ thông tin.");

            if (request.Title.Length > StoryTitleMaxBusinessLength)
                throw new ArgumentException($"Tiêu đề vượt quá giới hạn cho phép (tối đa {StoryTitleMaxBusinessLength} ký tự).");

            if (string.IsNullOrWhiteSpace(coverImageUrl))
                throw new InvalidOperationException("Vui lòng điền đầy đủ thông tin.");

            if (!string.IsNullOrEmpty(request.Summary) && request.Summary.Length > StorySummaryMaxLength)
                throw new ArgumentException($"Mô tả truyện vượt quá giới hạn cho phép (tối đa {StorySummaryMaxLength} ký tự).");

            // Nhiều truyện được phép cùng tiêu đề hiển thị: slug suy từ title có thể trùng → thêm hậu tố số cho đến khi unique (UTCID02).
            var baseSlug = GenerateSlug(request.Title ?? string.Empty);
            var slug = AllocateUniqueSlug(baseSlug, excludeStoryId: null);

            var validAgeRatings = new[] { "ALL", "13+", "16+", "18+" };
            if (!validAgeRatings.Contains(request.AgeRating?.ToUpper()))
            {
                throw new ArgumentException($"Invalid age rating. Must be one of: {string.Join(", ", validAgeRatings)}");
            }

            if (string.IsNullOrWhiteSpace(request.StoryProgressStatus))
                throw new InvalidOperationException("Vui lòng điền đầy đủ thông tin.");

            var validProgressStatuses = new[] { "ONGOING", "COMPLETED", "HIATUS" };
            var progressStatus = request.StoryProgressStatus.Trim().ToUpperInvariant();
            if (!validProgressStatuses.Contains(progressStatus))
            {
                throw new ArgumentException($"Invalid story progress status. Must be one of: {string.Join(", ", validProgressStatuses)}");
            }

            // Truyện mới (Create) bắt buộc tiến độ «Đang ra» — không cho COMPLETED/HIATUS lúc khởi tạo.
            if (progressStatus != "ONGOING")
                throw new InvalidOperationException("Truyện mới chỉ được tạo với trạng thái tiến độ Đang ra.");

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
            // Create: return lightweight DTO without extra DB lookups (counts, author profile, etc.).
            // This keeps Create() unit-testable without requiring a real database.
            return MapToResponseDto(story, includeComputedLookups: false, categoryIdsOverride: request.CategoryIds);
        }

        public PagedResultDto<StoryListItemDto> GetAll(StoryQueryDto query)
        {
            var storiesQuery = _storyRepository.GetAll();

            if (!query.IncludeComplianceHiddenInLists)
                storiesQuery = storiesQuery.Where(s => !s.compliance_hidden);

            if (query.ExcludeBannedAuthors)
            {
                storiesQuery = storiesQuery.Where(s =>
                    s.author == null
                    || s.author.status == null
                    || s.author.status.ToUpper() != "BANNED");
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

            if (!string.IsNullOrWhiteSpace(query.StoryProgressStatus))
            {
                var ps = query.StoryProgressStatus.Trim().ToUpperInvariant();
                storiesQuery = storiesQuery.Where(s =>
                    s.story_progress_status != null &&
                    s.story_progress_status.Trim().ToUpper() == ps);
            }

            if (!string.IsNullOrWhiteSpace(query.AgeRating))
            {
                var ar = query.AgeRating.Trim().ToUpperInvariant();
                storiesQuery = storiesQuery.Where(s =>
                    (s.age_rating ?? "ALL").Trim().ToUpper() == ar);
            }

            if (query.MinTotalChapters.HasValue)
            {
                var min = query.MinTotalChapters.Value;
                storiesQuery = storiesQuery.Where(s => (s.total_chapters ?? 0) >= min);
            }

            if (query.MaxTotalChapters.HasValue)
            {
                var max = query.MaxTotalChapters.Value;
                storiesQuery = storiesQuery.Where(s => (s.total_chapters ?? 0) <= max);
            }

            if (query.UsesAi.HasValue)
            {
                if (query.UsesAi.Value)
                {
                    storiesQuery = storiesQuery.Where(s => s.chapters.Any(c =>
                        c.status != null &&
                        c.status.Trim().ToUpper() == "PUBLISHED" &&
                        (
                            (c.ai_contribution_ratio.HasValue && c.ai_contribution_ratio.Value > 0) ||
                            (c.ai_similarity_percent.HasValue && c.ai_similarity_percent.Value > 0)
                        )));
                }
                else
                {
                    storiesQuery = storiesQuery.Where(s => !s.chapters.Any(c =>
                        c.status != null &&
                        c.status.Trim().ToUpper() == "PUBLISHED" &&
                        (
                            (c.ai_contribution_ratio.HasValue && c.ai_contribution_ratio.Value > 0) ||
                            (c.ai_similarity_percent.HasValue && c.ai_similarity_percent.Value > 0)
                        )));
                }
            }

            if (query.StatusIn != null && query.StatusIn.Count > 0)
            {
                var statusList = query.StatusIn.Select(s => s?.Trim().ToUpperInvariant()).Where(s => !string.IsNullOrEmpty(s)).ToList();
                if (statusList.Count > 0)
                    storiesQuery = storiesQuery.Where(s => s.status != null && statusList.Contains(s.status.ToUpperInvariant()));
            }
            else if (!string.IsNullOrWhiteSpace(query.Status))
            {
                storiesQuery = storiesQuery.Where(s => s.status == query.Status);
            }

            var hasSearch = !string.IsNullOrWhiteSpace(query.Search);
            var sortBy = query.SortBy?.ToLower();
            var sortOrder = query.SortOrder == "asc" ? "asc" : "desc";

            if (!hasSearch)
            {
                storiesQuery = sortBy switch
                {
                    "updated_at" => sortOrder == "asc"
                        ? storiesQuery.OrderBy(s => s.updated_at)
                        : storiesQuery.OrderByDescending(s => s.updated_at),
                    "total_views" => sortOrder == "asc"
                        ? storiesQuery.OrderBy(s => s.total_views)
                        : storiesQuery.OrderByDescending(s => s.total_views),
                    "avg_rating" => sortOrder == "asc"
                        ? storiesQuery.OrderBy(s => s.avg_rating)
                        : storiesQuery.OrderByDescending(s => s.avg_rating),
                    _ => sortOrder == "asc"
                        ? storiesQuery.OrderBy(s => s.created_at)
                        : storiesQuery.OrderByDescending(s => s.created_at)
                };

                var totalCount = storiesQuery.Count();

                var stories = storiesQuery
                    .Skip((query.Page - 1) * query.PageSize)
                    .Take(query.PageSize)
                    .ToList();

                var authorIdsForAvatars = stories
                    .Select(s => s.author_id)
                    .Where(id => id.HasValue)
                    .Select(id => id!.Value)
                    .Distinct()
                    .ToList();
                var authorAvatarByUserId = UserProfileDAO.GetAvatarUrlsByUserIds(authorIdsForAvatars);

                return new PagedResultDto<StoryListItemDto>
                {
                    Items = stories.Select(s => MapToListItemDto(s, authorAvatarByUserId)).ToList(),
                    TotalCount = totalCount,
                    Page = query.Page,
                    PageSize = query.PageSize
                };
            }

            var rawSearch = query.Search ?? string.Empty;
            var normalizedSearch = NormalizeSearchText(rawSearch);
            var searchTokens = normalizedSearch
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToArray();

            var allFilteredStories = storiesQuery.ToList();
            var authorDisplayNameByUserId = allFilteredStories
                .Where(s => s.author_id.HasValue)
                .Select(s => s.author_id!.Value)
                .Distinct()
                .ToDictionary(id => id, id => NotificationDAO.GetUserDisplayName(id) ?? string.Empty);

            var searchedWithRank = allFilteredStories
                .Select(s => new
                {
                    Story = s,
                    Rank = GetSearchRank(s, normalizedSearch, searchTokens, authorDisplayNameByUserId)
                })
                .Where(x => x.Rank < int.MaxValue)
                .ToList();

            // Nếu đã có truyện match theo tác giả (rank 0) thì ưu tiên tuyệt đối nhóm này,
            // tránh lẫn các truyện chỉ match title/summary/email khi user đang tìm tên tác giả.
            if (searchedWithRank.Any(x => x.Rank == 0))
            {
                searchedWithRank = searchedWithRank
                    .Where(x => x.Rank == 0)
                    .ToList();
            }

            var rankedStories = sortBy switch
            {
                "updated_at" => sortOrder == "asc"
                    ? searchedWithRank.OrderBy(x => x.Rank).ThenBy(x => x.Story.updated_at).ToList()
                    : searchedWithRank.OrderBy(x => x.Rank).ThenByDescending(x => x.Story.updated_at).ToList(),
                "total_views" => sortOrder == "asc"
                    ? searchedWithRank.OrderBy(x => x.Rank).ThenBy(x => x.Story.total_views).ToList()
                    : searchedWithRank.OrderBy(x => x.Rank).ThenByDescending(x => x.Story.total_views).ToList(),
                "avg_rating" => sortOrder == "asc"
                    ? searchedWithRank.OrderBy(x => x.Rank).ThenBy(x => x.Story.avg_rating).ToList()
                    : searchedWithRank.OrderBy(x => x.Rank).ThenByDescending(x => x.Story.avg_rating).ToList(),
                _ => sortOrder == "asc"
                    ? searchedWithRank.OrderBy(x => x.Rank).ThenBy(x => x.Story.created_at).ToList()
                    : searchedWithRank.OrderBy(x => x.Rank).ThenByDescending(x => x.Story.created_at).ToList()
            };

            var searchedTotalCount = rankedStories.Count;
            var pagedStories = rankedStories
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => x.Story)
                .ToList();

            var authorIdsForAvatarsInPage = pagedStories
                .Select(s => s.author_id)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            var authorAvatarByUserIdInPage = UserProfileDAO.GetAvatarUrlsByUserIds(authorIdsForAvatarsInPage);

            return new PagedResultDto<StoryListItemDto>
            {
                Items = pagedStories.Select(s => MapToListItemDto(s, authorAvatarByUserIdInPage)).ToList(),
                TotalCount = searchedTotalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        private static string NormalizeSearchText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var normalized = text.Trim().Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var ch in normalized)
            {
                var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (uc != UnicodeCategory.NonSpacingMark)
                    sb.Append(ch);
            }
            return sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        }

        private static bool ContainsAllTokens(string haystack, string[] tokens)
        {
            if (tokens.Length == 0) return false;
            return tokens.All(t => haystack.Contains(t));
        }

        private static int GetSearchRank(
            stories story,
            string normalizedSearch,
            string[] tokens,
            IReadOnlyDictionary<Guid, string> authorDisplayNameByUserId)
        {
            var authorDisplayName = story.author_id.HasValue && authorDisplayNameByUserId.TryGetValue(story.author_id.Value, out var name)
                ? name
                : string.Empty;
            var authorHaystack = NormalizeSearchText(string.Join(" ", new[]
            {
                authorDisplayName,
                story.author?.user_profiles?.nickname ?? string.Empty
            }));
            var titleHaystack = NormalizeSearchText(story.title ?? string.Empty);
            var summaryHaystack = NormalizeSearchText(story.summary ?? string.Empty);
            var emailHaystack = NormalizeSearchText(story.author?.email ?? string.Empty);

            var authorMatch = (!string.IsNullOrEmpty(normalizedSearch) && authorHaystack.Contains(normalizedSearch))
                || ContainsAllTokens(authorHaystack, tokens);
            if (authorMatch) return 0;

            // Ưu tiên truyện match theo thông tin tác giả trước, nhưng vẫn cho phép
            // tìm theo tên truyện để không làm mất hành vi search cũ.
            var titleMatch = (!string.IsNullOrEmpty(normalizedSearch) && titleHaystack.Contains(normalizedSearch))
                || ContainsAllTokens(titleHaystack, tokens);
            if (titleMatch) return 1;

            var summaryMatch = (!string.IsNullOrEmpty(normalizedSearch) && summaryHaystack.Contains(normalizedSearch))
                || ContainsAllTokens(summaryHaystack, tokens);
            if (summaryMatch) return 2;

            var emailMatch = (!string.IsNullOrEmpty(normalizedSearch) && emailHaystack.Contains(normalizedSearch))
                || ContainsAllTokens(emailHaystack, tokens);
            if (emailMatch) return 3;

            return int.MaxValue;
        }

        public StoryResponseDto? GetById(Guid id, Guid? userId = null)
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
            if (userId.HasValue && userId.Value != Guid.Empty)
            {
                var (chapterId, lastReadAt) = UserLibraryDAO.GetLastRead(userId.Value, id);
                if (chapterId.HasValue)
                {
                    dto.LastReadChapterId = chapterId;
                    dto.LastReadAt = lastReadAt;
                    var ch = ChapterDAO.GetById(chapterId.Value);
                    if (ch != null) dto.LastReadChapterTitle = ch.title;
                }
            }
            return dto;
        }

        public StoryResponseDto? GetBySlug(string slug, Guid? userId = null)
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
            if (userId.HasValue && userId.Value != Guid.Empty && story != null)
            {
                var (chapterId, lastReadAt) = UserLibraryDAO.GetLastRead(userId.Value, story.id);
                if (chapterId.HasValue)
                {
                    dto.LastReadChapterId = chapterId;
                    dto.LastReadAt = lastReadAt;
                    var ch = ChapterDAO.GetById(chapterId.Value);
                    if (ch != null) dto.LastReadChapterTitle = ch.title;
                }
            }
            return dto;
        }

        public void SaveReadingProgress(Guid storyId, Guid userId, Guid chapterId)
        {
            if (storyId == Guid.Empty || userId == Guid.Empty || chapterId == Guid.Empty) return;
            var story = _storyRepository.GetById(storyId);
            if (story == null || !string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase)) return;
            UserLibraryDAO.SaveReadingProgress(userId, storyId, chapterId);
        }

        public PagedResultDto<StoryListItemDto> GetByAuthor(Guid authorId, StoryQueryDto query)
        {
            var authorQuery = new StoryQueryDto
            {
                Page = query.Page,
                PageSize = query.PageSize,
                Search = query.Search,
                CategoryId = query.CategoryId,
                CategoryIds = query.CategoryIds,
                ExcludeStoryIds = query.ExcludeStoryIds,
                IncludeStoryIds = query.IncludeStoryIds,
                AlsoIncludeStoryIds = query.AlsoIncludeStoryIds,
                AuthorId = authorId,
                Status = query.Status,
                StatusIn = query.StatusIn,
                SortBy = query.SortBy,
                SortOrder = query.SortOrder,
                StoryProgressStatus = query.StoryProgressStatus,
                AgeRating = query.AgeRating,
                MinTotalChapters = query.MinTotalChapters,
                MaxTotalChapters = query.MaxTotalChapters,
                UsesAi = query.UsesAi,
                IncludeComplianceHiddenInLists = query.IncludeComplianceHiddenInLists,
                ExcludeBannedAuthors = query.ExcludeBannedAuthors,
            };

            return GetAll(authorQuery);
        }

        public bool Update(Guid id, UpdateStoryRequestDto request)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;
            if (story.author_id is Guid aid && _userLookup.IsAuthorWritingSuspended(aid))
                throw new InvalidOperationException("Tài khoản đang bị tạm khóa chức năng viết truyện (compliance/admin).");

            var prevStatus = (story.status ?? "").Trim().ToUpperInvariant();

            // Lưu version (vết tích) trước khi sửa nếu story đã public
            if (string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
            {
                StoryVersionDAO.SaveVersion(story, request.ChangeSummary);
            }

            if (request.CategoryIds != null && request.CategoryIds.Any())
            {
                foreach (var categoryId in request.CategoryIds)
                {
                    var category = _categoryLookup.GetById(categoryId);
                    if (category == null)
                        throw new InvalidOperationException($"Category with ID {categoryId} not found.");
                    if (!category.is_active ?? false)
                        throw new InvalidOperationException($"Category '{category.name}' is not active.");
                }
            }

            if (request.Title != story.title)
            {
                var newBaseSlug = GenerateSlug(request.Title ?? string.Empty);
                story.slug = AllocateUniqueSlug(newBaseSlug, id);
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

            var currentPublishStatus = (story.status ?? "").Trim().ToUpperInvariant();
            var currentProgressStatus = (story.story_progress_status ?? "ONGOING").Trim().ToUpperInvariant();
            var requestedProgressStatus = string.IsNullOrWhiteSpace(request.StoryProgressStatus)
                ? currentProgressStatus
                : request.StoryProgressStatus.Trim().ToUpperInvariant();

            // Truyện đã hoàn thành thì không được đổi ngược về Đang ra/Tạm dừng.
            if (currentProgressStatus == "COMPLETED" && requestedProgressStatus != "COMPLETED")
                throw new InvalidOperationException("Truyện đã ở trạng thái Hoàn thành, không thể chuyển về Đang ra hoặc Tạm dừng.");

            // Khi truyện đã public: chỉ cho cập nhật trạng thái tiến độ (Đang ra/Hoàn thành/Tạm dừng).
            if (currentPublishStatus == "PUBLISHED")
            {
                if (!string.Equals(request.Title?.Trim(), story.title?.Trim(), StringComparison.Ordinal))
                    throw new InvalidOperationException("Truyện đã xuất bản: không được sửa tên truyện.");
                if (!string.Equals(request.Summary?.Trim() ?? "", story.summary?.Trim() ?? "", StringComparison.Ordinal))
                    throw new InvalidOperationException("Truyện đã xuất bản: không được sửa mô tả truyện.");
                if (!string.IsNullOrWhiteSpace(request.AgeRating) &&
                    !string.Equals(request.AgeRating.Trim().ToUpperInvariant(), (story.age_rating ?? "ALL").Trim().ToUpperInvariant(), StringComparison.Ordinal))
                    throw new InvalidOperationException("Truyện đã xuất bản: không được sửa giới hạn độ tuổi.");
                if (!string.IsNullOrWhiteSpace(request.CoverImageUrl))
                    throw new InvalidOperationException("Truyện đã xuất bản: không được sửa ảnh bìa ở màn quản lý thông tin.");
                if (!string.IsNullOrWhiteSpace(request.Status) &&
                    !string.Equals(request.Status.Trim().ToUpperInvariant(), currentPublishStatus, StringComparison.Ordinal))
                    throw new InvalidOperationException("Truyện đã xuất bản: không được sửa trạng thái xuất bản.");

                var incomingCategoryIds = (request.CategoryIds ?? new List<Guid>()).Distinct().OrderBy(x => x).ToList();
                var currentCategoryIds = (story.category?.Select(c => c.id).Distinct().OrderBy(x => x).ToList()) ?? new List<Guid>();
                if (!incomingCategoryIds.SequenceEqual(currentCategoryIds))
                    throw new InvalidOperationException("Truyện đã xuất bản: không được sửa thể loại chi tiết.");
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

            var newStatus = (story.status ?? "").Trim().ToUpperInvariant();
            if (newStatus == "PENDING_REVIEW" && prevStatus != "PENDING_REVIEW")
                story.submitted_for_review_at = DateTime.UtcNow;
            else if (prevStatus == "PENDING_REVIEW" && newStatus != "PENDING_REVIEW")
                story.submitted_for_review_at = null;

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
            if (story.author_id is Guid aid && _userLookup.IsAuthorWritingSuspended(aid))
                throw new InvalidOperationException("Tài khoản đang bị tạm khóa chức năng viết truyện (compliance/admin).");

            var statusUpper = (story.status ?? "").Trim().ToUpperInvariant();
            if (statusUpper != "DRAFT")
                throw new InvalidOperationException("Chỉ được xóa truyện khi ở trạng thái Bản nháp. Truyện hiện tại: " + (story.status ?? "—"));

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

                if (story.author_id is Guid aid && _userLookup.IsAuthorWritingSuspended(aid))
                    throw new InvalidOperationException("Tài khoản đang bị tạm khóa chức năng viết truyện (compliance/admin), không thể gửi xuất bản.");
                var progress = (story.story_progress_status ?? "ONGOING").Trim().ToUpperInvariant();
                if (progress == "HIATUS" || progress == "COMPLETED")
                    throw new InvalidOperationException($"Truyện đang ở trạng thái {(progress == "COMPLETED" ? "Hoàn thành" : "Tạm dừng")}, không thể gửi xuất bản.");

                // Author "Publish" = gửi chờ duyệt. Chỉ moderator approve mới chuyển sang PUBLISHED.
                story.status = "PENDING_REVIEW";
                story.updated_at = DateTime.Now;
                story.submitted_for_review_at = DateTime.UtcNow;
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

            if (story.author_id is Guid aidUnpub && _userLookup.IsAuthorWritingSuspended(aidUnpub))
                throw new InvalidOperationException("Tài khoản đang bị tạm khóa chức năng viết truyện (compliance/admin).");

            story.status = "DRAFT";
            story.updated_at = DateTime.Now;
            story.submitted_for_review_at = null;

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

            RatingDAO.CreateOnce(userId, storyId, starValue, reviewText, status: "VISIBLE");
            var (avg, count) = RatingDAO.GetAverageAndCount(storyId, status: "VISIBLE");
            StoryDAO.UpdateAvgRating(storyId, avg);
            return (avg, count);
        }

        public (string? reason, DateTime? rejectedAt) GetLatestRejectionForStory(Guid storyId)
        {
            return ModerationLogDAO.GetLatestRejection("STORY", storyId);
        }

        public CommunityStatsDto GetPublicCommunityStats()
        {
            // Khớp danh sách truyện công khai (GET /stories): không tính truyện tác giả BANNED —
            // số truyện / lượt xem / tác giả phải đồng bộ với nội dung khách thực sự thấy.
            var publishedVisibleStories = _storyRepository
                .GetAll()
                // EF Core không dịch được string.Equals(..., StringComparison) sang SQL.
                .Where(s => s.status != null && s.status.ToUpper() == "PUBLISHED")
                .Where(s => !s.compliance_hidden)
                .Where(s =>
                    s.author == null
                    || s.author.status == null
                    || s.author.status.ToUpper() != "BANNED");

            return new CommunityStatsDto
            {
                PublishedStoriesCount = publishedVisibleStories.Count(),
                AuthorsCount = _userLookup.CountAuthorsExcludingBanned(),
                TotalViews = publishedVisibleStories.Sum(s => (long?)(s.total_views ?? 0)) ?? 0
            };
        }

        /// <summary>Slug duy nhất trong DB; <paramref name="excludeStoryId"/> dùng khi đổi title (giữ/ghép slug cho đúng bản ghi hiện tại).</summary>
        private string AllocateUniqueSlug(string baseSlug, Guid? excludeStoryId)
        {
            var normalizedBase = string.IsNullOrWhiteSpace(baseSlug) ? "story" : baseSlug.Trim();

            for (var attempt = 0; attempt < 10_000; attempt++)
            {
                string candidate;
                if (attempt == 0)
                    candidate = TruncateSlugForSuffix(normalizedBase, StorySlugMaxLength, suffixToAppend: string.Empty);
                else
                {
                    var suffix = "-" + (attempt + 1);
                    var trunc = TruncateSlugForSuffix(normalizedBase, StorySlugMaxLength, suffix);
                    candidate = trunc + suffix;
                }

                var existing = _storyRepository.GetBySlug(candidate);
                if (existing == null)
                    return candidate;
                if (excludeStoryId.HasValue && existing.id == excludeStoryId.Value)
                    return candidate;
            }

            throw new InvalidOperationException("Không thể tạo slug truyện duy nhất sau nhiều lần thử.");
        }

        private static string TruncateSlugForSuffix(string slugBase, int maxTotalLength, string suffixToAppend)
        {
            if (string.IsNullOrEmpty(slugBase))
                return "story";
            var maxBase = maxTotalLength - suffixToAppend.Length;
            if (maxBase < 1)
                maxBase = 1;
            if (slugBase.Length <= maxBase)
                return slugBase;
            return slugBase.Substring(0, maxBase).TrimEnd('-');
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

        private StoryResponseDto MapToResponseDto(
            stories story,
            bool includeComputedLookups = true,
            IReadOnlyCollection<Guid>? categoryIdsOverride = null)
        {
            var categories = story.category?.ToList() ?? new List<categories>();
            var categoryIds = categoryIdsOverride?.ToList() ?? categories.Select(c => c.id).ToList();
            var categoryNames = categories.Any() ? string.Join(", ", categories.Select(c => c.name)) : null;

            var totalChapters = story.total_chapters ?? 0;
            var publishedChaptersCount = 0;
            var totalComments = 0;
            var totalFavorites = 0;
            DateTime? latestUpdatedAt = story.updated_at;
            string? authorName = null;
            string? authorAvatarUrl = null;
            var usesAi = false;

            if (includeComputedLookups)
            {
                totalChapters = story.total_chapters ?? ChapterDAO.GetCountByStoryId(story.id);
                publishedChaptersCount = ChapterDAO.GetPublishedCountByStoryId(story.id);
                totalComments = CommentDAO.GetCountByStoryId(story.id);
                // Theo dõi thực tế theo user_library (FOLLOW) để tránh lệch với cột denormalized total_favorites.
                totalFavorites = UserLibraryDAO.GetFollowCountByStoryId(story.id);
                var latestChapterUpdatedAt = ChapterDAO.GetLatestUpdatedAtByStoryId(story.id);
                latestUpdatedAt = story.updated_at;
                if (latestChapterUpdatedAt.HasValue && (!latestUpdatedAt.HasValue || latestChapterUpdatedAt > latestUpdatedAt))
                    latestUpdatedAt = latestChapterUpdatedAt;

                authorName = story.author_id.HasValue ? NotificationDAO.GetUserDisplayName(story.author_id.Value) : null;
                authorAvatarUrl = story.author_id.HasValue ? UserProfileDAO.GetAvatarUrlForUser(story.author_id.Value) : null;
                usesAi = _chapterRepository
                    .GetByStoryId(story.id)
                    .Any(c =>
                        string.Equals(c.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase) &&
                        (
                            (c.ai_similarity_percent.HasValue && c.ai_similarity_percent.Value > 0) ||
                            (c.ai_contribution_ratio.HasValue && c.ai_contribution_ratio.Value > 0)
                        ));
            }
            string? authorAccountStatus = null;
            if (includeComputedLookups && story.author_id.HasValue)
                authorAccountStatus = UserDAO.GetAccountStatus(story.author_id.Value);
            return new StoryResponseDto
            {
                Id = story.id,
                Title = story.title,
                Slug = story.slug,
                Summary = story.summary,
                CategoryIds = categoryIds,
                CategoryNames = categoryNames,
                AuthorId = story.author_id,
                AuthorName = authorName,
                AuthorAccountStatus = authorAccountStatus,
                AuthorAvatarUrl = authorAvatarUrl,
                CoverImage = story.cover_image,
                Status = story.status,
                StoryProgressStatus = story.story_progress_status,
                AgeRating = story.age_rating,
                TotalChapters = totalChapters,
                PublishedChaptersCount = publishedChaptersCount,
                TotalViews = story.total_views,
                TotalComments = totalComments,
                TotalFavorites = totalFavorites,
                AvgRating = story.avg_rating,
                WordCount = story.word_count,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                LatestUpdatedAt = latestUpdatedAt,
                PublishedAt = story.published_at,
                LastPublishedAt = story.last_published_at,
                CommentsDisabled = story.comments_disabled,
                ComplianceHidden = story.compliance_hidden,
                ComplianceFlagged = story.compliance_flagged,
                UsesAi = usesAi
            };
        }

        private StoryListItemDto MapToListItemDto(stories story, IReadOnlyDictionary<Guid, string?>? authorAvatarByUserId = null)
        {
            var categories = story.category?.ToList() ?? new List<categories>();
            var categoryIds = categories.Select(c => c.id).ToList();
            var categoryNames = categories.Any() ? string.Join(", ", categories.Select(c => c.name)) : null;

            var totalChapters = story.total_chapters ?? ChapterDAO.GetCountByStoryId(story.id);
            var publishedChaptersCount = ChapterDAO.GetPublishedCountByStoryId(story.id);
            var totalComments = CommentDAO.GetCountByStoryId(story.id);
            // Theo dõi thực tế theo user_library (FOLLOW) để tránh lệch với cột denormalized total_favorites.
            var totalFavorites = UserLibraryDAO.GetFollowCountByStoryId(story.id);
            var latestChapterUpdatedAt = ChapterDAO.GetLatestUpdatedAtByStoryId(story.id);
            var latestUpdatedAt = story.updated_at;
            if (latestChapterUpdatedAt.HasValue && (!latestUpdatedAt.HasValue || latestChapterUpdatedAt > latestUpdatedAt))
                latestUpdatedAt = latestChapterUpdatedAt;

            // Tên hiển thị tác giả cho danh sách công khai (guest) — cùng nguồn với chi tiết truyện.
            var authorName = story.author_id.HasValue ? NotificationDAO.GetUserDisplayName(story.author_id.Value) : null;
            string? authorAvatarUrl = null;
            if (story.author_id.HasValue && authorAvatarByUserId != null &&
                authorAvatarByUserId.TryGetValue(story.author_id.Value, out var av))
                authorAvatarUrl = av;

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
                AuthorName = authorName,
                AuthorAvatarUrl = authorAvatarUrl,
                AgeRating = story.age_rating,
                TotalChapters = totalChapters,
                PublishedChaptersCount = publishedChaptersCount,
                TotalViews = story.total_views,
                TotalComments = totalComments,
                TotalFavorites = totalFavorites,
                AvgRating = story.avg_rating,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                LatestUpdatedAt = latestUpdatedAt
            };
        }
    }
}

//dotnet test --filter "FullyQualifiedName~AIStory.Tests.UT01_FunctionCreateStory"