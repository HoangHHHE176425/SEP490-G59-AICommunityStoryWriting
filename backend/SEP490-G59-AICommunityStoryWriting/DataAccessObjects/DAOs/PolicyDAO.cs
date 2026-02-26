using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.Queries;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;

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

        public async Task<(IEnumerable<system_policies> Items, int TotalCount)> GetPoliciesAsync(
            StoryPlatformDbContext context,
            AdminPolicyQuery query)
        {
            var q = context.system_policies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(query.Type))
            {
                var typeUpper = query.Type.Trim().ToUpperInvariant();
                q = q.Where(p => p.type != null && p.type.ToUpper() == typeUpper);
            }

            if (query.IsActive.HasValue)
            {
                q = q.Where(p => (p.is_active ?? false) == query.IsActive.Value);
            }

            if (!string.IsNullOrWhiteSpace(query.Search))
            {
                var s = query.Search.Trim().ToLowerInvariant();
                q = q.Where(p =>
                    (p.type != null && p.type.ToLower().Contains(s)) ||
                    p.version.ToLower().Contains(s) ||
                    p.content.ToLower().Contains(s));
            }

            var total = await q.CountAsync();

            var sortBy = (query.SortBy ?? "").Trim().ToLowerInvariant();
            var asc = (query.SortOrder ?? "desc").Trim().ToLowerInvariant() == "asc";

            q = sortBy switch
            {
                "type" => asc ? q.OrderBy(p => p.type) : q.OrderByDescending(p => p.type),
                "version" => asc ? q.OrderBy(p => p.version) : q.OrderByDescending(p => p.version),
                "activated_at" => asc ? q.OrderBy(p => p.activated_at) : q.OrderByDescending(p => p.activated_at),
                _ => asc ? q.OrderBy(p => p.created_at) : q.OrderByDescending(p => p.created_at)
            };

            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

            var items = await q.AsNoTracking()
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task AddPolicyAsync(StoryPlatformDbContext context, system_policies policy)
        {
            context.system_policies.Add(policy);
            await context.SaveChangesAsync();
        }

        public async Task UpdatePolicyAsync(StoryPlatformDbContext context, system_policies policy)
        {
            context.system_policies.Update(policy);
            await context.SaveChangesAsync();
        }

        public async Task DeletePolicyAsync(StoryPlatformDbContext context, system_policies policy)
        {
            context.system_policies.Remove(policy);
            await context.SaveChangesAsync();
        }

        public async Task DeactivateOtherPoliciesOfTypeAsync(StoryPlatformDbContext context, string? type, Guid keepActiveId)
        {
            if (string.IsNullOrWhiteSpace(type)) return;
            var typeUpper = type.Trim().ToUpperInvariant();

            var others = await context.system_policies
                .Where(p => p.id != keepActiveId && p.type != null && p.type.ToUpper() == typeUpper && p.is_active == true)
                .ToListAsync();

            if (others.Count == 0) return;

            foreach (var p in others)
            {
                p.is_active = false;
            }

            await context.SaveChangesAsync();
        }

        public async Task<(int Total, int Active, Dictionary<string, int> ByType)> GetStatsAsync(StoryPlatformDbContext context)
        {
            var total = await context.system_policies.CountAsync();
            var active = await context.system_policies.CountAsync(p => p.is_active == true);

            var rows = await context.system_policies
                .GroupBy(p => p.type)
                .Select(g => new { Type = g.Key, Count = g.Count() })
                .ToListAsync();

            var byType = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var r in rows)
            {
                var key = string.IsNullOrWhiteSpace(r.Type) ? "UNKNOWN" : r.Type.Trim().ToUpperInvariant();
                byType[key] = r.Count;
            }

            return (total, active, byType);
        }
    }
}

