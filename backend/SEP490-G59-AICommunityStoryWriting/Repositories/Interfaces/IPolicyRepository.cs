using BusinessObjects.Entities;
using DataAccessObjects.Queries;
using System.Collections.Generic;

namespace Repositories.Interfaces
{
    public interface IPolicyRepository
    {
        Task<system_policies?> GetPolicyByIdAsync(Guid id);
        Task<system_policies?> GetActivePolicyByTypeAsync(string type);

        Task<(IEnumerable<system_policies> Items, int TotalCount)> GetPoliciesAsync(AdminPolicyQuery query);
        Task AddPolicyAsync(system_policies policy);
        Task UpdatePolicyAsync(system_policies policy);
        Task DeletePolicyAsync(system_policies policy);
        Task DeactivateOtherPoliciesOfTypeAsync(string type, Guid keepActiveId);

        Task<(int Total, int Active, Dictionary<string, int> ByType)> GetStatsAsync();
    }
}

