using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Chạy phân tích memory chương trong nền sau khi chương được lưu/xuất bản (không chặn HTTP).</summary>
public static class ChapterMemoryAnalysisScheduler
{
    private const int MinContentLength = 80;
    //chạy phân tích chương ở nền
    public static void TrySchedule(
        IServiceScopeFactory? scopeFactory,
        ILogger logger,
        Guid storyId,
        Guid chapterId,
        string? chapterTitle,
        int orderIndex,
        string? chapterContent)
    {
        if (scopeFactory == null) return;
        var content = chapterContent?.Trim();
        if (string.IsNullOrEmpty(content) || content.Length < MinContentLength) return;

        var title = string.IsNullOrWhiteSpace(chapterTitle) ? "(Không tiêu đề)" : chapterTitle.Trim();

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var svc = scope.ServiceProvider.GetService(typeof(IChapterMemoryAnalysisService)) as IChapterMemoryAnalysisService;
                if (svc == null) return;

                await svc.ExtractAndPersistAsync(storyId, chapterId, title, orderIndex, content, CancellationToken.None)
                    .ConfigureAwait(false);
                logger.LogInformation(
                    "Chapter memory analysis completed StoryId={StoryId} ChapterId={ChapterId}",
                    storyId, chapterId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Chapter memory analysis failed StoryId={StoryId} ChapterId={ChapterId}",
                    storyId, chapterId);
            }
        });
    }
}
