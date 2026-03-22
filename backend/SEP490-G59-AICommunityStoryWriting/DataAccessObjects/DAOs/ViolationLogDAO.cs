using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class ViolationLogDAO
{
    public static Guid Insert(
        Guid? complianceOfficerId,
        Guid? violatorId,
        string? targetType,
        Guid? targetId,
        string? penaltyType,
        string? reason,
        string? policyReference)
    {
        using var context = new StoryPlatformDbContext();
        var row = new violation_logs
        {
            id = Guid.NewGuid(),
            compliance_officer_id = complianceOfficerId,
            violator_id = violatorId,
            target_type = string.IsNullOrWhiteSpace(targetType) ? null : targetType.Trim(),
            target_id = targetId,
            penalty_type = string.IsNullOrWhiteSpace(penaltyType) ? null : penaltyType.Trim(),
            reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            policy_reference = string.IsNullOrWhiteSpace(policyReference) ? null : policyReference.Trim(),
            created_at = DateTime.UtcNow
        };
        context.violation_logs.Add(row);
        context.SaveChanges();
        return row.id;
    }

    public static List<violation_logs> ListByViolator(Guid violatorId, int take = 100)
    {
        take = take < 1 ? 50 : (take > 500 ? 500 : take);
        using var context = new StoryPlatformDbContext();
        return context.violation_logs.AsNoTracking()
            .Include(v => v.compliance_officer)
            .ThenInclude(u => u!.user_profiles)
            .Where(v => v.violator_id == violatorId)
            .OrderByDescending(v => v.created_at)
            .Take(take)
            .ToList();
    }
}
