using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories;
using Services.DTOs.Chapters;
using Services.DTOs.Stories;

namespace Services.Implementations
{
    public class ChapterService : IChapterService
    {
        private readonly IChapterRepository _chapterRepository;

        public ChapterService(IChapterRepository chapterRepository)
        {
            _chapterRepository = chapterRepository;
        }

        public ChapterResponseDto Create(CreateChapterRequestDto request)
        {
            var story = StoryDAO.GetById(request.StoryId);
            if (story == null)
            {
                throw new InvalidOperationException($"Story with ID {request.StoryId} not found.");
            }

            var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(request.StoryId, request.OrderIndex);
            if (existingChapter != null)
            {
                throw new InvalidOperationException($"Chapter with order index {request.OrderIndex} already exists for this story.");
            }

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
            if (accessType == "FREE" && coinPrice > 0)
            {
                coinPrice = 0; // Force coin price to 0 for FREE chapters
            }

            var wordCount = CalculateWordCount(request.Content);

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
                id = Guid.NewGuid(),
                story_id = request.StoryId,
                title = request.Title,
                content = request.Content,
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

            _chapterRepository.Add(chapter);

            try
            {
                UpdateStoryChapterStats(request.StoryId);

                // If chapter is published, update story's last_published_at
                if (status == "PUBLISHED" && story != null)
                {
                    story.last_published_at = DateTime.Now;
                    StoryDAO.Update(story);
                }
            }
            catch (Exception)
            {
                // Log error but don't fail the create operation
                // The chapter was already created successfully
            }

            var createdChapter = _chapterRepository.GetById(chapter.id);
            return MapToResponseDto(createdChapter!);
        }

        public PagedResultDto<ChapterListItemDto> GetAll(ChapterQueryDto query)
        {
            var chaptersQuery = _chapterRepository.GetAll();

            if (query.StoryId.HasValue)
            {
                chaptersQuery = chaptersQuery.Where(c => c.story_id == query.StoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Status))
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
                "published_at" => query.SortOrder == "asc"
                    ? chaptersQuery.OrderBy(c => c.published_at)
                    : chaptersQuery.OrderByDescending(c => c.published_at),
                _ => query.SortOrder == "asc"
                    ? chaptersQuery.OrderBy(c => c.order_index)
                    : chaptersQuery.OrderByDescending(c => c.order_index)
            };

            var totalCount = chaptersQuery.Count();

            var chapterList = chaptersQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new PagedResultDto<ChapterListItemDto>
            {
                Items = chapterList.Select(MapToListItemDto),
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public ChapterResponseDto? GetById(Guid id)
        {
            var chapter = _chapterRepository.GetById(id);
            return chapter == null ? null : MapToResponseDto(chapter);
        }

        public IEnumerable<ChapterListItemDto> GetByStoryId(Guid storyId)
        {
            var chapterList = _chapterRepository.GetByStoryId(storyId)
                .OrderBy(c => c.order_index)
                .ToList();

            return chapterList.Select(MapToListItemDto);
        }

        public ChapterResponseDto? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex)
        {
            var chapter = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, orderIndex);
            return chapter == null ? null : MapToResponseDto(chapter);
        }

        public bool Update(Guid id, UpdateChapterRequestDto request)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            if (request.OrderIndex.HasValue && request.OrderIndex.Value != chapter.order_index)
            {
                var storyId = chapter.story_id ?? Guid.Empty;
                var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, request.OrderIndex.Value);
                if (existingChapter != null && existingChapter.id != id)
                {
                    throw new InvalidOperationException($"Chapter with order index {request.OrderIndex.Value} already exists for this story.");
                }
            }

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
            chapter.content = request.Content;
            chapter.updated_at = DateTime.Now;

            if (request.OrderIndex.HasValue)
                chapter.order_index = request.OrderIndex.Value;

            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var newStatus = request.Status.ToUpper();
                var oldStatus = chapter.status?.ToUpper() ?? "DRAFT";

                chapter.status = newStatus;

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

            if (request.Content != null)
            {
                chapter.word_count = CalculateWordCount(request.Content);
            }

            _chapterRepository.Update(chapter);

            if (chapter.story_id.HasValue)
            {
                try
                {
                    UpdateStoryChapterStats(chapter.story_id.Value);

                    // If chapter status was changed to PUBLISHED, update story's last_published_at
                    if (!string.IsNullOrWhiteSpace(request.Status) && request.Status.ToUpper() == "PUBLISHED")
                    {
                        var story = StoryDAO.GetById(chapter.story_id.Value);
                        if (story != null)
                        {
                            story.last_published_at = DateTime.Now;
                            StoryDAO.Update(story);
                        }
                    }
                }
                catch (Exception)
                {
                    // Log error but don't fail the update operation
                    // The chapter was already updated successfully
                }
            }

            return true;
        }

        public bool Delete(Guid id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            var storyId = chapter.story_id;

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

            chapter.status = "PUBLISHED";
            chapter.published_at = DateTime.Now;
            chapter.updated_at = DateTime.Now;

            _chapterRepository.Update(chapter);

            if (chapter.story_id.HasValue)
            {
                try
                {
                    var story = StoryDAO.GetById(chapter.story_id.Value);
                    if (story != null)
                    {
                        story.last_published_at = DateTime.Now;
                        StoryDAO.Update(story);
                    }
                }
                catch (Exception)
                {
                    // Log error but don't fail the publish operation
                    // The chapter was already published successfully
                }
            }

            return true;
        }

        public bool Unpublish(Guid id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            chapter.status = "DRAFT";
            chapter.updated_at = DateTime.Now;

            _chapterRepository.Update(chapter);
            return true;
        }

        public bool Reorder(Guid id, int newOrderIndex)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

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

        private int CalculateWordCount(string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return 0;

            return content
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }

        private void UpdateStoryChapterStats(Guid storyId)
        {
            var story = StoryDAO.GetById(storyId);
            if (story == null)
                return;

            var chapterList = _chapterRepository.GetByStoryId(storyId).ToList();

            story.total_chapters = chapterList.Count;
            story.word_count = chapterList.Sum(c => c.word_count ?? 0);
            story.updated_at = DateTime.Now;

            StoryDAO.Update(story);
        }

        private ChapterResponseDto MapToResponseDto(chapters chapter)
        {
            stories? story = null;
            if (chapter.story_id.HasValue)
                story = StoryDAO.GetById(chapter.story_id.Value);

            return new ChapterResponseDto
            {
                Id = chapter.id,
                StoryId = chapter.story_id,
                StoryTitle = story?.title,
                Title = chapter.title,
                OrderIndex = chapter.order_index,
                Content = chapter.content,
                Status = chapter.status,
                AccessType = chapter.access_type,
                CoinPrice = chapter.coin_price,
                WordCount = chapter.word_count,
                AiContributionRatio = chapter.ai_contribution_ratio,
                IsAiClean = chapter.is_ai_clean ?? false,
                PublishedAt = chapter.published_at,
                CreatedAt = chapter.created_at,
                UpdatedAt = chapter.updated_at
            };
        }

        private ChapterListItemDto MapToListItemDto(chapters chapter)
        {
            return new ChapterListItemDto
            {
                Id = chapter.id,
                StoryId = chapter.story_id,
                Title = chapter.title,
                OrderIndex = chapter.order_index,
                Status = chapter.status,
                AccessType = chapter.access_type,
                CoinPrice = chapter.coin_price,
                WordCount = chapter.word_count,
                PublishedAt = chapter.published_at,
                CreatedAt = chapter.created_at
            };
        }
    }
}
