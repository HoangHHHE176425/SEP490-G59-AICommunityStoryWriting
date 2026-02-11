using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class AuthorPolicyAcceptanceDAO
    {
        private static AuthorPolicyAcceptanceDAO instance = null;
        private static readonly object instanceLock = new object();
        public static AuthorPolicyAcceptanceDAO Instance
        {
            get { lock (instanceLock) { return instance ??= new AuthorPolicyAcceptanceDAO(); } }
        }

        public async Task<author_policy_acceptances?> GetAcceptanceAsync(StoryPlatformDbContext context, Guid userId, Guid policyId)
        {
            return await context.author_policy_acceptances
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.user_id == userId && x.policy_id == policyId);
        }

        public async Task AddAcceptanceAsync(StoryPlatformDbContext context, author_policy_acceptances row)
        {
            context.author_policy_acceptances.Add(row);
            await context.SaveChangesAsync();
        }
    }
}

