using Services.DTOs.AI;

namespace Services.Interfaces;

public interface IChapterVersionAiCompareService
{
    Task<CompareChapterVersionToAiResponse> CompareVersionSnapshotToAiAsync(
        CompareChapterVersionToAiRequest request,
        Guid? userId,
        CancellationToken cancellationToken = default);
}
