using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class PolicyDAO
    {
        private static PolicyDAO instance = null;
        private static readonly object instanceLock = new object();
        public static PolicyDAO Instance
        {
            get { lock (instanceLock) { return instance ??= new PolicyDAO(); } }
        }

        public async Task<system_policies?> GetPolicyByIdAsync(StoryPlatformDbContext context, Guid id)
        {
            return await context.system_policies.AsNoTracking().FirstOrDefaultAsync(p => p.id == id);
        }

        public async Task<system_policies?> GetActivePolicyByTypeAsync(StoryPlatformDbContext context, string type)
        {
            var typeUpper = (type ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(typeUpper)) return null;

            return await context.system_policies
                .AsNoTracking()
                .Where(p => p.is_active == true && p.type != null && p.type.ToUpper() == typeUpper)
                .OrderByDescending(p => p.activated_at ?? p.created_at)
                .ThenByDescending(p => p.created_at)
                .FirstOrDefaultAsync();
        }
    }
}

