using System.Linq;
using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>So sánh nội dung tác giả với <c>ai_output</c> cùng <c>story_id</c>; <c>chapter_index</c> chuẩn = <c>order_index</c> (0-based), có fallback <c>order_index+1</c> cho dữ liệu cũ. Không ghi DB — % lưu khi tạo/cập nhật chương qua <c>AiSimilarityPercent</c>.</summary>
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
        if (orderIndex < 0)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chương có order_index không hợp lệ." };

        var story = _storyRepository.GetById(storyId);
        if (story == null)
            return new CompareChapterResponse { HasBothContents = false, Message = "Truyện không tồn tại." };

        if (userId.HasValue && story.author_id != userId.Value)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chỉ tác giả truyện được so sánh." };

        var authorContent = (chapter.content ?? "").Trim();
        return await CompareAuthorContentWithAiRecordsAsync(authorContent, storyId, orderIndex, cancellationToken);
    }

    public async Task<CompareChapterResponse> ComparePreviewAsync(CompareChapterPreviewRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (request.StoryId == Guid.Empty)
            return new CompareChapterResponse { HasBothContents = false, Message = "StoryId không hợp lệ." };

        var story = _storyRepository.GetById(request.StoryId);
        if (story == null)
            return new CompareChapterResponse { HasBothContents = false, Message = "Truyện không tồn tại." };

        if (userId.HasValue && story.author_id != userId.Value)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chỉ tác giả truyện được so sánh." };

        if (request.OrderIndex < 0)
            return new CompareChapterResponse { HasBothContents = false, Message = "Thứ tự chương (order_index) không hợp lệ." };

        var authorContent = (request.Content ?? "").Trim();
        return await CompareAuthorContentWithAiRecordsAsync(authorContent, request.StoryId, request.OrderIndex, cancellationToken);
    }

    private async Task<CompareChapterResponse> CompareAuthorContentWithAiRecordsAsync(
        string authorContent,
        Guid storyId,
        int orderIndex,
        CancellationToken cancellationToken)
    {
        var aiRecords = _aiContentRepository.GetAllByStoryIdAndChapterIndex(storyId, orderIndex);
        if (aiRecords.Count == 0 && orderIndex == 0)
            aiRecords = _aiContentRepository.GetAllByStoryIdAndChapterIndex(storyId, 1);
        // Legacy / nhầm lẫn: một số bản ghi lưu chapter_index = số chương hiển thị (1-based, vd. 6) thay vì order_index (0-based, vd. 5).
        if (aiRecords.Count == 0 && orderIndex > 0)
            aiRecords = _aiContentRepository.GetAllByStoryIdAndChapterIndex(storyId, orderIndex + 1);

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

