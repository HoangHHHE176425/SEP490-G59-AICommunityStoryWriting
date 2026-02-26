using BusinessObjects.Entities;

namespace Repositories.Interfaces
{
    public interface IAuthorPolicyAcceptanceRepository
    {
        Task<author_policy_acceptances?> GetAcceptanceAsync(Guid userId, Guid policyId);
        Task AddAcceptanceAsync(author_policy_acceptances row);
        Task<int> CountByPolicyAsync(Guid policyId);
    }
}

