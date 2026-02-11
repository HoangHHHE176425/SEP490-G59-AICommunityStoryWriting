using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

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
    }
}

