using BusinessObjects.Entities;

namespace Repositories.Interfaces
{
    public interface IAIUsageLogRepository
    {
        void Log(ai_usage_logs log);

        int? GetMaxTotalTokensForStoryAndActionType(Guid storyId, string actionType);

        int? GetMaxTotalTokensForStoryAndActionTypes(Guid storyId, IReadOnlyCollection<string> actionTypes);

        int SumCoCreatePipelineStepMaxTotals(
            Guid storyId,
            string actionOutline,
            string actionDraft,
            string actionExpand,
            string actionCorrect,
            string legacyCombinedWrite);
    }
}
