using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using DataAccessObjects.Queries;
using Repositories.Interfaces;
using System.Collections.Generic;

namespace Repositories.Implementations
{
    public class PolicyRepository : IPolicyRepository
    {
        private readonly StoryPlatformDbContext _context;

        public PolicyRepository(StoryPlatformDbContext context)
        {
            _context = context;
        }

        public Task<system_policies?> GetPolicyByIdAsync(Guid id)
            => PolicyDAO.Instance.GetPolicyByIdAsync(_context, id);

        public Task<system_policies?> GetActivePolicyByTypeAsync(string type)
            => PolicyDAO.Instance.GetActivePolicyByTypeAsync(_context, type);

        public Task<(IEnumerable<system_policies> Items, int TotalCount)> GetPoliciesAsync(AdminPolicyQuery query)
            => PolicyDAO.Instance.GetPoliciesAsync(_context, query);

        public Task AddPolicyAsync(system_policies policy)
            => PolicyDAO.Instance.AddPolicyAsync(_context, policy);

        public Task UpdatePolicyAsync(system_policies policy)
            => PolicyDAO.Instance.UpdatePolicyAsync(_context, policy);

        public Task DeletePolicyAsync(system_policies policy)
            => PolicyDAO.Instance.DeletePolicyAsync(_context, policy);

        public Task DeactivateOtherPoliciesOfTypeAsync(string type, Guid keepActiveId)
            => PolicyDAO.Instance.DeactivateOtherPoliciesOfTypeAsync(_context, type, keepActiveId);

        public Task<(int Total, int Active, Dictionary<string, int> ByType)> GetStatsAsync()
            => PolicyDAO.Instance.GetStatsAsync(_context);
    }
}

