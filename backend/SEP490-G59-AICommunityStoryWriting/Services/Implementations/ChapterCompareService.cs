using System.Linq;
using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>So sánh nội dung truyền vào với <c>ai_output</c> của chính <c>chapter_id</c>. Không ghi DB.</summary>
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
        {
            var draftRecords = _aiContentRepository.GetAllByDraftChapterId(request.ChapterId);
            if (draftRecords.Count == 0)
                return new CompareChapterResponse { HasBothContents = false, Message = "Không tìm thấy chương." };

            var storyId = draftRecords.FirstOrDefault()?.story_id;
            if (!storyId.HasValue)
                return new CompareChapterResponse { HasBothContents = false, Message = "Không xác định được truyện từ dữ liệu AI." };

            var draftStory = _storyRepository.GetById(storyId.Value);
            if (draftStory == null)
                return new CompareChapterResponse { HasBothContents = false, Message = "Truyện không tồn tại." };
            if (userId.HasValue && draftStory.author_id != userId.Value)
                return new CompareChapterResponse { HasBothContents = false, Message = "Chỉ tác giả truyện được so sánh." };

            var authorDraftContent = (request.Content ?? "").Trim();
            return await CompareAuthorContentWithAiRecordsAsync(authorDraftContent, draftRecords, cancellationToken);
        }

        if (!chapter.story_id.HasValue)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chương không gắn truyện." };

        var story = _storyRepository.GetById(chapter.story_id.Value);
        if (story == null)
            return new CompareChapterResponse { HasBothContents = false, Message = "Truyện không tồn tại." };

        if (userId.HasValue && story.author_id != userId.Value)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chỉ tác giả truyện được so sánh." };

        var authorContent = (request.Content ?? "").Trim();
        var chapterRecords = _aiContentRepository.GetAllByChapterId(request.ChapterId);
        return await CompareAuthorContentWithAiRecordsAsync(authorContent, chapterRecords, cancellationToken);
    }

    private async Task<CompareChapterResponse> CompareAuthorContentWithAiRecordsAsync(
        string authorContent,
        IReadOnlyList<BusinessObjects.Entities.ai_generated_content> aiRecords,
        CancellationToken cancellationToken)
    {
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
                Message = "Chưa có bản nội dung AI cho chapter này."
            };

        // Compare against all AI outputs for this chapter and keep the highest similarity score.
        var aiOutputStrings = aiRecords
            .OrderByDescending(r => r.created_at)
            .Select(r => r.ai_output)
            .ToList();
        var (bestScore, bestAiLength) = await ContentSimilarityHelper.CompareAuthorToAiOutputsAsync(
            authorContent,
            aiOutputStrings,
            _configuration,
            cancellationToken);

        var threshold = _configuration.GetValue("ChapterCompare:SimilarityThresholdPercent", SimilarityThresholdPercent);
        var roundedScore = Math.Round(bestScore, 2);

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

