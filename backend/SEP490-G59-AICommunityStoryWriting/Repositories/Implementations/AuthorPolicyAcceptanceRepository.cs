using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class AuthorPolicyAcceptanceRepository : IAuthorPolicyAcceptanceRepository
    {
        private readonly StoryPlatformDbContext _context;

        public AuthorPolicyAcceptanceRepository(StoryPlatformDbContext context)
        {
            _context = context;
        }

        public Task<author_policy_acceptances?> GetAcceptanceAsync(Guid userId, Guid policyId)
            => AuthorPolicyAcceptanceDAO.Instance.GetAcceptanceAsync(_context, userId, policyId);

        public Task AddAcceptanceAsync(author_policy_acceptances row)
            => AuthorPolicyAcceptanceDAO.Instance.AddAcceptanceAsync(_context, row);
    }
}

