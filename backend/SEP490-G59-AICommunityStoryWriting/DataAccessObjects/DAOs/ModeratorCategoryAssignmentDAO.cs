using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class ModeratorCategoryAssignmentDAO
    {
        private static ModeratorCategoryAssignmentDAO instance = null;
        private static readonly object instanceLock = new object();
        public static ModeratorCategoryAssignmentDAO Instance
        {
            get { lock (instanceLock) { return instance ??= new ModeratorCategoryAssignmentDAO(); } }
        }

        public async Task<List<Guid>> GetCategoryIdsAsync(StoryPlatformDbContext context, Guid moderatorId)
        {
            return await context.moderator_category_assignments
                .AsNoTracking()
                .Where(x => x.moderator_id == moderatorId)
                .Select(x => x.category_id)
                .ToListAsync();
        }

        public async Task ReplaceAssignmentsAsync(StoryPlatformDbContext context, Guid moderatorId, IReadOnlyCollection<Guid> categoryIds)
        {
            // Remove existing
            var existing = await context.moderator_category_assignments
                .Where(x => x.moderator_id == moderatorId)
                .ToListAsync();
            if (existing.Count > 0)
            {
                context.moderator_category_assignments.RemoveRange(existing);
            }

            // Insert new
            foreach (var cid in categoryIds.Distinct())
            {
                context.moderator_category_assignments.Add(new moderator_category_assignments
                {
                    moderator_id = moderatorId,
                    category_id = cid,
                    assigned_at = DateTime.UtcNow
                });
            }

            await context.SaveChangesAsync();
        }
    }
}

