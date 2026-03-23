using System.Linq;
using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>So sánh <c>chapters.content</c> theo <c>ChapterId</c> (tự lấy story + order_index) với <c>ai_output</c> cùng <c>story_id</c> và <c>chapter_index</c>; lấy điểm cao nhất.</summary>
public class ChapterCompareService : IChapterCompareService
{
    private const double SimilarityThresholdPercent = 85.0;

    private readonly IChapterRepository _chapterRepository;
    private readonly IAiGeneratedContentRepository _aiContentRepository;
    private readonly IStoryRepository _storyRepository;
    private readonly IConfiguration _configuration;

    public ChapterCompareService(
        IChapterRepository chapterRepository,
        IAiGeneratedContentRepository aiContentRepository,
        IStoryRepository storyRepository,
        IConfiguration configuration)
    {
        _chapterRepository = chapterRepository;
        _aiContentRepository = aiContentRepository;
        _storyRepository = storyRepository;
        _configuration = configuration;
    }

    public async Task<CompareChapterResponse> CompareAsync(CompareChapterRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        var chapter = _chapterRepository.GetById(request.ChapterId);
        if (chapter == null)
            return new CompareChapterResponse { HasBothContents = false, Message = "Không tìm thấy chương." };

        if (!chapter.story_id.HasValue)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chương không gắn truyện." };

        var storyId = chapter.story_id.Value;
        var orderIndex = chapter.order_index;
        if (orderIndex < 1)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chương có order_index không hợp lệ." };

        var story = _storyRepository.GetById(storyId);
        if (story == null)
            return new CompareChapterResponse { HasBothContents = false, Message = "Truyện không tồn tại." };

        if (userId.HasValue && story.author_id != userId.Value)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chỉ tác giả truyện được so sánh." };

        var authorContent = (chapter.content ?? "").Trim();
        var aiRecords = _aiContentRepository.GetAllByStoryIdAndChapterIndex(storyId, orderIndex);

        if (string.IsNullOrEmpty(authorContent))
            return new CompareChapterResponse
            {
                HasBothContents = false,
                AuthorContentLength = 0,
                AiContentLength = 0,
                Message = "Nội dung chương trống."
            };
        if (aiRecords.Count == 0)
            return new CompareChapterResponse
            {
                HasBothContents = false,
                AuthorContentLength = authorContent.Length,
                AiContentLength = 0,
                Message = "Chưa có bản nội dung AI (co-create) cho thứ tự chương này (chapter_index)."
            };

        var aiOutputStrings = aiRecords.Select(r => r.ai_output).ToList();
        var (bestScore, bestAiLength) = await ContentSimilarityHelper.CompareAuthorToAiOutputsAsync(
            authorContent,
            aiOutputStrings,
            _configuration,
            cancellationToken);

        var threshold = _configuration.GetValue("ChapterCompare:SimilarityThresholdPercent", SimilarityThresholdPercent);
        var roundedScore = Math.Round(bestScore, 2);

        if (string.Equals(chapter.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
        {
            chapter.ai_similarity_percent = (decimal)roundedScore;
            _chapterRepository.Update(chapter);
        }

        return new CompareChapterResponse
        {
            SimilarityScore = roundedScore,
            IsSimilar = bestScore >= threshold,
            AuthorContentLength = authorContent.Length,
            AiContentLength = bestAiLength,
            HasBothContents = true,
            Message = bestScore >= threshold ? "Nội dung chương rất giống với bản AI." : "Nội dung chương khác so với bản AI."
        };
    }
}
