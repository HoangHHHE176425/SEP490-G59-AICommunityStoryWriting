using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories;
using Services.DTOs.Stories;

namespace Services.Implementations
{
    public class StoryService : IStoryService
    {
        private readonly IStoryRepository _storyRepository;

        public StoryService(IStoryRepository storyRepository)
        {
            _storyRepository = storyRepository;
        }

        public StoryResponseDto Create(CreateStoryRequestDto request, int authorId, string? coverImageUrl)
        {
            // Validate category exists
            var category = CategoryDAO.GetById(request.CategoryId);
            if (category == null)
            {
                throw new InvalidOperationException($"Category with ID {request.CategoryId} not found.");
            }

            if (!category.is_active ?? false)
            {
                throw new InvalidOperationException($"Category '{category.name}' is not active.");
            }

            // Generate slug and check for duplicates
            var slug = GenerateSlug(request.Title);
            var existingStory = _storyRepository.GetBySlug(slug);
            if (existingStory != null)
            {
                throw new InvalidOperationException($"Story with slug '{slug}' already exists.");
            }

            // Validate age rating
            var validAgeRatings = new[] { "ALL", "13+", "16+", "18+" };
            if (!validAgeRatings.Contains(request.AgeRating?.ToUpper()))
            {
                throw new ArgumentException($"Invalid age rating. Must be one of: {string.Join(", ", validAgeRatings)}");
            }

            var story = new story
            {
                title = request.Title,
                slug = slug,
                summary = request.Summary,
                category_id = request.CategoryId,
                author_id = authorId,
                cover_image = coverImageUrl,

                status = "DRAFT",
                sale_type = "CHAPTER",
                access_type = "FREE",

                expected_chapters = request.ExpectedChapters,
                release_frequency_days = request.ReleaseFrequencyDays,
                age_rating = request.AgeRating.ToUpper(),

                total_chapters = 0,
                total_views = 0,
                total_favorites = 0,
                avg_rating = 0,
                word_count = 0,

                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            _storyRepository.Add(story);

            // Reload to get navigation properties
            var createdStory = _storyRepository.GetById(story.id);
            return MapToResponseDto(createdStory!);
        }

        public PagedResultDto<StoryListItemDto> GetAll(StoryQueryDto query)
        {
            var storiesQuery = _storyRepository.GetAll();

            // Apply filters
            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var searchLower = query.Search.ToLower();
                storiesQuery = storiesQuery.Where(s =>
                    s.title.ToLower().Contains(searchLower) ||
                    (s.summary != null && s.summary.ToLower().Contains(searchLower)));
            }

            if (query.CategoryId.HasValue)
            {
                storiesQuery = storiesQuery.Where(s => s.category_id == query.CategoryId.Value);
            }

            if (query.AuthorId.HasValue)
            {
                storiesQuery = storiesQuery.Where(s => s.author_id == query.AuthorId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
            {
                storiesQuery = storiesQuery.Where(s => s.status == query.Status);
            }

            // Apply sorting
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

            // Apply pagination
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

        public StoryResponseDto? GetById(int id)
        {
            var story = _storyRepository.GetById(id);
            return story == null ? null : MapToResponseDto(story);
        }

        public StoryResponseDto? GetBySlug(string slug)
        {
            var story = _storyRepository.GetBySlug(slug);
            return story == null ? null : MapToResponseDto(story);
        }

        public PagedResultDto<StoryListItemDto> GetByAuthor(int authorId, StoryQueryDto query)
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

        public bool Update(int id, UpdateStoryRequestDto request)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            // Validate category if changed
            if (request.CategoryId != story.category_id)
            {
                var category = CategoryDAO.GetById(request.CategoryId);
                if (category == null)
                {
                    throw new InvalidOperationException($"Category with ID {request.CategoryId} not found.");
                }

                if (!category.is_active ?? false)
                {
                    throw new InvalidOperationException($"Category '{category.name}' is not active.");
                }
            }

            // Check slug conflict if title changed
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

            // Validate status if provided
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var validStatuses = new[] { "DRAFT", "PUBLISHED", "COMPLETED", "CANCELLED" };
                if (!validStatuses.Contains(request.Status.ToUpper()))
                {
                    throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                }
            }

            // Validate age rating if provided
            if (!string.IsNullOrWhiteSpace(request.AgeRating))
            {
                var validAgeRatings = new[] { "ALL", "13+", "16+", "18+" };
                if (!validAgeRatings.Contains(request.AgeRating.ToUpper()))
                {
                    throw new ArgumentException($"Invalid age rating. Must be one of: {string.Join(", ", validAgeRatings)}");
                }
            }

            story.title = request.Title;
            story.summary = request.Summary;
            story.category_id = request.CategoryId;
            story.updated_at = DateTime.Now;

            // Update cover image if provided
            if (!string.IsNullOrWhiteSpace(request.CoverImageUrl))
            {
                story.cover_image = request.CoverImageUrl;
            }

            if (!string.IsNullOrWhiteSpace(request.Status))
                story.status = request.Status.ToUpper();

            if (!string.IsNullOrWhiteSpace(request.SaleType))
                story.sale_type = request.SaleType.ToUpper();

            if (!string.IsNullOrWhiteSpace(request.AccessType))
                story.access_type = request.AccessType.ToUpper();

            if (!string.IsNullOrWhiteSpace(request.AgeRating))
                story.age_rating = request.AgeRating.ToUpper();

            if (request.ExpectedChapters.HasValue)
                story.expected_chapters = request.ExpectedChapters.Value;

            if (request.ReleaseFrequencyDays.HasValue)
                story.release_frequency_days = request.ReleaseFrequencyDays.Value;

            _storyRepository.Update(story);
            return true;
        }

        public bool Delete(int id)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            // Check if story has chapters
            var chapterCount = ChapterDAO.GetAll()
                .Where(c => c.story_id == id)
                .Count();

            if (chapterCount > 0)
            {
                throw new InvalidOperationException("Cannot delete story that has associated chapters.");
            }

            _storyRepository.Delete(id);
            return true;
        }

        public bool Publish(int id)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            story.status = "PUBLISHED";
            story.published_at = DateTime.Now;
            story.updated_at = DateTime.Now;

            _storyRepository.Update(story);
            return true;
        }

        public bool Unpublish(int id)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            story.status = "DRAFT";
            story.updated_at = DateTime.Now;

            _storyRepository.Update(story);
            return true;
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

        private StoryResponseDto MapToResponseDto(story story)
        {
            var category = story.category_id.HasValue
                ? CategoryDAO.GetById(story.category_id.Value)
                : null;

            return new StoryResponseDto
            {
                Id = story.id,
                Title = story.title,
                Slug = story.slug,
                Summary = story.summary,
                CategoryId = story.category_id,
                CategoryName = category?.name,
                AuthorId = story.author_id,
                CoverImage = story.cover_image,
                Status = story.status,
                SaleType = story.sale_type,
                AccessType = story.access_type,
                AgeRating = story.age_rating,
                ExpectedChapters = story.expected_chapters,
                ReleaseFrequencyDays = story.release_frequency_days,
                TotalChapters = story.total_chapters,
                TotalViews = story.total_views,
                TotalFavorites = story.total_favorites,
                AvgRating = story.avg_rating,
                WordCount = story.word_count,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at,
                PublishedAt = story.published_at,
                LastPublishedAt = story.last_published_at
            };
        }

        private StoryListItemDto MapToListItemDto(story story)
        {
            var category = story.category_id.HasValue
                ? CategoryDAO.GetById(story.category_id.Value)
                : null;

            return new StoryListItemDto
            {
                Id = story.id,
                Title = story.title,
                Slug = story.slug,
                Summary = story.summary,
                Status = story.status,
                CoverImage = story.cover_image,
                CategoryId = story.category_id,
                CategoryName = category?.name,
                AuthorId = story.author_id,
                TotalChapters = story.total_chapters,
                TotalViews = story.total_views,
                TotalFavorites = story.total_favorites,
                AvgRating = story.avg_rating,
                CreatedAt = story.created_at,
                UpdatedAt = story.updated_at
            };
        }
    }
}
