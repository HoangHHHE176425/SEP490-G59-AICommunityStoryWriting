using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.EntityFrameworkCore;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class AIUsageLogRepository : IAIUsageLogRepository
    {
        public void Log(ai_usage_logs log)
        {
            AIUsageLogDAO.Add(log);
        }

        public int? GetMaxTotalTokensForStoryAndActionType(Guid storyId, string actionType)
        {
            if (storyId == Guid.Empty || string.IsNullOrWhiteSpace(actionType))
                return null;
            using var ctx = new StoryPlatformDbContext();
            var at = actionType.Trim();
            var query = ctx.ai_usage_logs.AsNoTracking()
                .Where(l => l.story_id == storyId && l.action_type == at && l.total_tokens != null && l.total_tokens > 0)
                .Select(l => l.total_tokens!.Value);
            return query.Any() ? query.Max() : null;
        }

        public int? GetMaxTotalTokensForStoryAndActionTypes(Guid storyId, IReadOnlyCollection<string> actionTypes)
        {
            if (storyId == Guid.Empty || actionTypes == null || actionTypes.Count == 0)
                return null;
            var set = actionTypes.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToHashSet(StringComparer.Ordinal);
            if (set.Count == 0)
                return null;
            using var ctx = new StoryPlatformDbContext();
            var query = ctx.ai_usage_logs.AsNoTracking()
                .Where(l => l.story_id == storyId && l.action_type != null && set.Contains(l.action_type) && l.total_tokens != null && l.total_tokens > 0)
                .Select(l => l.total_tokens!.Value);
            return query.Any() ? query.Max() : null;
        }

        public int SumCoCreatePipelineStepMaxTotals(
            Guid storyId,
            string actionOutline,
            string actionDraft,
            string actionExpand,
            string actionCorrect,
            string legacyCombinedWrite)
        {
            var o = GetMaxTotalTokensForStoryAndActionType(storyId, actionOutline) ?? 0;
            var d = GetMaxTotalTokensForStoryAndActionType(storyId, actionDraft) ?? 0;
            var e = GetMaxTotalTokensForStoryAndActionType(storyId, actionExpand) ?? 0;
            var c = GetMaxTotalTokensForStoryAndActionType(storyId, actionCorrect) ?? 0;
            var leg = GetMaxTotalTokensForStoryAndActionType(storyId, legacyCombinedWrite) ?? 0;
            var writePart = d + e;
            if (writePart == 0 && leg > 0)
                writePart = leg;
            return o + writePart + c;
        }
    }
}
