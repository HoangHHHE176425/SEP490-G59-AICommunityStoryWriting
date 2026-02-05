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
            if (!string.IsNullOrWhiteSpace(request.AccessType) && !validAccessTypes.Contains(request.AccessType.ToUpper()))
            {
                throw new ArgumentException($"Invalid access type. Must be one of: {string.Join(", ", validAccessTypes)}");
            }

            var wordCount = CalculateWordCount(request.Content);

            var chapter = new chapters
            {
                id = Guid.NewGuid(),
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

            UpdateStoryChapterStats(request.StoryId);

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
                var validStatuses = new[] { "DRAFT", "PUBLISHED", "ARCHIVED" };
                if (!validStatuses.Contains(request.Status.ToUpper()))
                {
                    throw new ArgumentException($"Invalid status. Must be one of: {string.Join(", ", validStatuses)}");
                }
            }

            if (!string.IsNullOrWhiteSpace(request.AccessType))
            {
                var validAccessTypes = new[] { "FREE", "PAID" };
                if (!validAccessTypes.Contains(request.AccessType.ToUpper()))
                {
                    throw new ArgumentException($"Invalid access type. Must be one of: {string.Join(", ", validAccessTypes)}");
                }
            }

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

            if (request.Content != null)
            {
                chapter.word_count = CalculateWordCount(request.Content);
            }

            _chapterRepository.Update(chapter);

            if (chapter.story_id.HasValue)
                UpdateStoryChapterStats(chapter.story_id.Value);

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
                UpdateStoryChapterStats(storyId.Value);

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
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story != null)
                {
                    story.last_published_at = DateTime.Now;
                    StoryDAO.Update(story);
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
