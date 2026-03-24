using System.Linq;
using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>So sánh <c>chapter_versions.content_snapshot</c> (bản mới nhất theo <c>version_number</c>) với <c>ai_generated_content</c> theo <c>chapter_id</c>.</summary>
public class ChapterVersionAiCompareService : IChapterVersionAiCompareService
{
    private const double SimilarityThresholdPercent = 85.0;

    private readonly IChapterRepository _chapterRepository;
    private readonly IChapterVersionRepository _versionRepository;
    private readonly IAiGeneratedContentRepository _aiContentRepository;
    private readonly IStoryRepository _storyRepository;
    private readonly IConfiguration _configuration;

    public ChapterVersionAiCompareService(
        IChapterRepository chapterRepository,
        IChapterVersionRepository versionRepository,
        IAiGeneratedContentRepository aiContentRepository,
        IStoryRepository storyRepository,
        IConfiguration configuration)
    {
        _chapterRepository = chapterRepository;
        _versionRepository = versionRepository;
        _aiContentRepository = aiContentRepository;
        _storyRepository = storyRepository;
        _configuration = configuration;
    }

    public async Task<CompareChapterVersionToAiResponse> CompareVersionSnapshotToAiAsync(
        CompareChapterVersionToAiRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default)
    {
        var chapter = _chapterRepository.GetById(request.ChapterId);
        if (chapter == null)
            return Fail("Không tìm thấy chương.");

        if (!chapter.story_id.HasValue)
            return Fail("Chương không gắn truyện.");

        var story = _storyRepository.GetById(chapter.story_id.Value);
        if (story == null)
            return Fail("Truyện không tồn tại.");

        if (userId.HasValue && story.author_id != userId.Value)
            return Fail("Chỉ tác giả truyện được gọi API này.");

        var versions = _versionRepository.GetByChapterId(request.ChapterId).ToList();
        var latest = versions.OrderByDescending(v => v.version_number).FirstOrDefault();
        if (latest == null)
            return Fail("Chưa có phiên bản (chapter_versions) cho chương này.");

        var snapshot = (latest.content_snapshot ?? "").Trim();
        var aiRecords = _aiContentRepository.GetAllByChapterId(request.ChapterId);

        if (string.IsNullOrEmpty(snapshot))
            return new CompareChapterVersionToAiResponse
            {
                HasBothContents = false,
                SnapshotContentLength = 0,
                AiContentLength = 0,
                VersionId = latest.id,
                VersionNumber = latest.version_number,
                Message = "content_snapshot trống."
            };

        if (aiRecords.Count == 0)
            return new CompareChapterVersionToAiResponse
            {
                HasBothContents = false,
                SnapshotContentLength = snapshot.Length,
                AiContentLength = 0,
                VersionId = latest.id,
                VersionNumber = latest.version_number,
                Message = "Chưa có bản ai_generated_content gắn chapter_id này."
            };

        // Reduce accidental high score from historical unrelated AI outputs:
        // use the newest AI record linked to this chapter as baseline.
        var aiOutputs = aiRecords
            .OrderByDescending(r => r.created_at)
            .Take(1)
            .Select(r => r.ai_output)
            .ToList();
        var (bestScore, bestAiLen) = await ContentSimilarityHelper.CompareAuthorToAiOutputsAsync(
            snapshot,
            aiOutputs,
            _configuration,
            cancellationToken);

        var threshold = _configuration.GetValue("ChapterCompare:SimilarityThresholdPercent", SimilarityThresholdPercent);
        var rounded = Math.Round(bestScore, 2);

        latest.ai_similarity_percent = (decimal)rounded;
        _versionRepository.Update(latest);

        return new CompareChapterVersionToAiResponse
        {
            SimilarityScore = rounded,
            IsSimilar = bestScore >= threshold,
            SnapshotContentLength = snapshot.Length,
            AiContentLength = bestAiLen,
            HasBothContents = true,
            VersionId = latest.id,
            VersionNumber = latest.version_number,
            Message = bestScore >= threshold
                ? "Snapshot rất giống với bản AI."
                : "Snapshot khác so với bản AI."
        };
    }

    private static CompareChapterVersionToAiResponse Fail(string message)
        => new() { HasBothContents = false, Message = message };
}
