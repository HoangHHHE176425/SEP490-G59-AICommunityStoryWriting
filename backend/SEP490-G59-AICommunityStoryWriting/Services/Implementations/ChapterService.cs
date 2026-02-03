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
            // Validate story exists
            var story = StoryDAO.GetById(request.StoryId);
            if (story == null)
            {
                throw new InvalidOperationException($"Story with ID {request.StoryId} not found.");
            }

            // Check if order_index already exists for this story
            var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(request.StoryId, request.OrderIndex);
            if (existingChapter != null)
            {
                throw new InvalidOperationException($"Chapter with order index {request.OrderIndex} already exists for this story.");
            }

            // Validate access type
            var validAccessTypes = new[] { "FREE", "PAID" };
            if (!string.IsNullOrWhiteSpace(request.AccessType) && !validAccessTypes.Contains(request.AccessType.ToUpper()))
            {
                throw new ArgumentException($"Invalid access type. Must be one of: {string.Join(", ", validAccessTypes)}");
            }

            // Calculate word count from content
            var wordCount = CalculateWordCount(request.Content);

            var chapter = new chapter
            {
                story_id = request.StoryId,
                title = request.Title,
                content = request.Content,
                order_index = request.OrderIndex,
                status = "DRAFT",
                access_type = request.AccessType?.ToUpper() ?? "FREE",
                coin_price = request.CoinPrice ?? 0,
                word_count = wordCount,
                ai_contribution_ratio = request.AiContributionRatio ?? 0,
                is_ai_clean = request.IsAiClean,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            _chapterRepository.Add(chapter);

            // Update story's total chapters and word count
            UpdateStoryChapterStats(request.StoryId);

            // Reload to get navigation properties
            var createdChapter = _chapterRepository.GetById(chapter.id);
            return MapToResponseDto(createdChapter!);
        }

        public PagedResultDto<ChapterListItemDto> GetAll(ChapterQueryDto query)
        {
            var chaptersQuery = _chapterRepository.GetAll();

            // Apply filters
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

            // Apply sorting
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

            // Apply pagination
            var chapters = chaptersQuery
                .Skip((query.Page - 1) * query.PageSize)
                .Take(query.PageSize)
                .ToList();

            return new PagedResultDto<ChapterListItemDto>
            {
                Items = chapters.Select(MapToListItemDto),
                TotalCount = totalCount,
                Page = query.Page,
                PageSize = query.PageSize
            };
        }

        public ChapterResponseDto? GetById(int id)
        {
            var chapter = _chapterRepository.GetById(id);
            return chapter == null ? null : MapToResponseDto(chapter);
        }

        public IEnumerable<ChapterListItemDto> GetByStoryId(int storyId)
        {
            var chapters = _chapterRepository.GetByStoryId(storyId)
                .OrderBy(c => c.order_index)
                .ToList();

            return chapters.Select(MapToListItemDto);
        }

        public ChapterResponseDto? GetByStoryIdAndOrderIndex(int storyId, int orderIndex)
        {
            var chapter = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, orderIndex);
            return chapter == null ? null : MapToResponseDto(chapter);
        }

        public bool Update(int id, UpdateChapterRequestDto request)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            // Check order_index conflict if changed
            if (request.OrderIndex.HasValue && request.OrderIndex.Value != chapter.order_index)
            {
                var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(chapter.story_id, request.OrderIndex.Value);
                if (existingChapter != null && existingChapter.id != id)
                {
                    throw new InvalidOperationException($"Chapter with order index {request.OrderIndex.Value} already exists for this story.");
                }
            }

            // Validate status if provided
            if (!string.IsNullOrWhiteSpace(request.Status))
            {
                var validStatuses = new[] { "DRAFT", "PUBLISHED", "ARCHIVED" };
                if (!validStatuses.Contains(request.Status.ToUpper()))
                {
                    throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                }
            }

            // Validate access type if provided
            if (!string.IsNullOrWhiteSpace(request.AccessType))
            {
                var validAccessTypes = new[] { "FREE", "PAID" };
                if (!validAccessTypes.Contains(request.AccessType.ToUpper()))
                {
                    throw new ArgumentException($"Invalid access type. Must be one of: {string.Join(", ", validAccessTypes)}");
                }
            }

            // Update fields
            chapter.title = request.Title;
            chapter.content = request.Content;
            chapter.updated_at = DateTime.Now;

            if (request.OrderIndex.HasValue)
                chapter.order_index = request.OrderIndex.Value;

            if (!string.IsNullOrWhiteSpace(request.Status))
                chapter.status = request.Status.ToUpper();

            if (!string.IsNullOrWhiteSpace(request.AccessType))
                chapter.access_type = request.AccessType.ToUpper();

            if (request.CoinPrice.HasValue)
                chapter.coin_price = request.CoinPrice.Value;

            if (request.AiContributionRatio.HasValue)
                chapter.ai_contribution_ratio = request.AiContributionRatio.Value;

            if (request.IsAiClean.HasValue)
                chapter.is_ai_clean = request.IsAiClean.Value;

            // Recalculate word count if content changed
            if (request.Content != null)
            {
                chapter.word_count = CalculateWordCount(request.Content);
            }

            _chapterRepository.Update(chapter);

            // Update story's total chapters and word count
            UpdateStoryChapterStats(chapter.story_id);

            return true;
        }

        public bool Delete(int id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            var storyId = chapter.story_id;

            _chapterRepository.Delete(id);

            // Update story's total chapters and word count
            UpdateStoryChapterStats(storyId);

            return true;
        }

        public bool Publish(int id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            chapter.status = "PUBLISHED";
            chapter.published_at = DateTime.Now;
            chapter.updated_at = DateTime.Now;

            _chapterRepository.Update(chapter);

            // Update story's last_published_at
            var story = StoryDAO.GetById(chapter.story_id);
            if (story != null)
            {
                story.last_published_at = DateTime.Now;
                StoryDAO.Update(story);
            }

            return true;
        }

        public bool Unpublish(int id)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            chapter.status = "DRAFT";
            chapter.updated_at = DateTime.Now;

            _chapterRepository.Update(chapter);
            return true;
        }

        public bool Reorder(int id, int newOrderIndex)
        {
            var chapter = _chapterRepository.GetById(id);
            if (chapter == null)
                return false;

            // Check if new order_index already exists
            var existingChapter = _chapterRepository.GetByStoryIdAndOrderIndex(chapter.story_id, newOrderIndex);
            if (existingChapter != null && existingChapter.id != id)
            {
                // Swap order indices
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

            // Simple word count: split by whitespace and count non-empty strings
            return content
                .Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Length;
        }

        private void UpdateStoryChapterStats(int storyId)
        {
            var story = StoryDAO.GetById(storyId);
            if (story == null)
                return;

            var chapters = _chapterRepository.GetByStoryId(storyId).ToList();

            story.total_chapters = chapters.Count;
            story.word_count = chapters.Sum(c => c.word_count ?? 0);
            story.updated_at = DateTime.Now;

            StoryDAO.Update(story);
        }

        private ChapterResponseDto MapToResponseDto(chapter chapter)
        {
            var story = StoryDAO.GetById(chapter.story_id);

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

        private ChapterListItemDto MapToListItemDto(chapter chapter)
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
