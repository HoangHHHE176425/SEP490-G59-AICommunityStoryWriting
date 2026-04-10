using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using Repositories;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Context builder: Story memory (N chương gần nhất).</summary>
public class StoryContextBuilder : IStoryContextBuilder
{
    private const int DefaultLastChapters = 5;
    private const int DefaultMaxCharsPerChapter = 2600;

    private readonly IStoryRepository _storyRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IConfiguration _configuration;

    public StoryContextBuilder(
        IStoryRepository storyRepository,
        IChapterRepository chapterRepository,
        IConfiguration configuration)
    {
        _storyRepository = storyRepository;
        _chapterRepository = chapterRepository;
        _configuration = configuration;
    }

    public string GetStoryAndMemoryBlock(Guid storyId, Guid? afterChapterId)
    {
        var story = _storyRepository.GetById(storyId);
        if (story == null)
            return string.Empty;

        var (lastN, maxCharsPerChapter) = GetConfig();
        var chapters = GetChaptersForContext(storyId, afterChapterId);
        var memoryBlock = StoryContextHelper.BuildLastChaptersContext(chapters, lastN, maxCharsPerChapter);

        var lines = new List<string>
        {
            $"## Truyện: {story.title}",
            string.IsNullOrWhiteSpace(story.summary) ? "" : $"Tóm tắt: {story.summary}",
            "## Nội dung các chương gần nhất (Story memory)",
            memoryBlock
        };
        return string.Join("\n\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public string BuildForSuggestNextChapter(Guid storyId, Guid? afterChapterId)
    {
        var story = _storyRepository.GetById(storyId);
        if (story == null)
            return string.Empty;

        var (lastN, maxCharsPerChapter) = GetConfig();
        var chapters = GetChaptersForContext(storyId, afterChapterId);
        var memoryBlock = StoryContextHelper.BuildLastChaptersContext(chapters, lastN, maxCharsPerChapter);

        var lines = new List<string>
        {
            $"## Truyện: {story.title}",
            string.IsNullOrWhiteSpace(story.summary) ? "" : $"Tóm tắt: {story.summary}",
            "## Nội dung các chương gần nhất (Story memory)",
            memoryBlock
        };
        return string.Join("\n\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    public string BuildForCheckConsistency(Guid storyId, string draftContent, Guid? afterChapterId, string? chapterTitle)
    {
        var story = _storyRepository.GetById(storyId);
        if (story == null)
            return string.Empty;

        var (lastN, maxCharsPerChapter) = GetConfig();
        var chapters = GetChaptersForContext(storyId, afterChapterId);
        var memoryBlock = StoryContextHelper.BuildLastChaptersContext(chapters, lastN, maxCharsPerChapter);

        var lines = new List<string>
        {
            $"## Truyện: {story.title}",
            string.IsNullOrWhiteSpace(story.summary) ? "" : $"Tóm tắt: {story.summary}",
            "## Nội dung các chương gần nhất (Story memory)",
            memoryBlock
        };
        if (!string.IsNullOrWhiteSpace(chapterTitle))
            lines.Add($"## Chương cần kiểm tra: {chapterTitle}");
        lines.Add("## Bản nháp cần kiểm tra");
        lines.Add(ChapterContentNormalizer.NormalizeForAi(draftContent, maxCharsPerChapter));
        return string.Join("\n\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
    }

    private (int lastN, int maxCharsPerChapter) GetConfig()
    {
        var lastN = _configuration.GetValue("StoryMemory:LastChapters", DefaultLastChapters);
        if (lastN < 1) lastN = DefaultLastChapters;
        var maxCharsPerChapter = _configuration.GetValue("StoryMemory:MaxCharsPerChapter", DefaultMaxCharsPerChapter);
        if (maxCharsPerChapter < 100) maxCharsPerChapter = DefaultMaxCharsPerChapter;
        return (lastN, maxCharsPerChapter);
    }

    private List<chapters> GetChaptersForContext(Guid storyId, Guid? afterChapterId)
    {
        var chapters = _chapterRepository.GetByStoryId(storyId).OrderBy(c => c.order_index).ToList();
        if (!afterChapterId.HasValue)
            return chapters;
        var afterIdx = chapters.FirstOrDefault(c => c.id == afterChapterId.Value)?.order_index;
        if (!afterIdx.HasValue)
            return chapters;
        return chapters.Where(c => c.order_index <= afterIdx.Value).ToList();
    }
}
