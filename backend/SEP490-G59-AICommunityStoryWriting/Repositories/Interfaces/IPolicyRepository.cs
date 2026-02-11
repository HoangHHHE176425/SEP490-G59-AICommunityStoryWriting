using BusinessObjects.Entities;

namespace Repositories.Interfaces
{
    public interface IPolicyRepository
    {
        Task<system_policies?> GetPolicyByIdAsync(Guid id);
        Task<system_policies?> GetActivePolicyByTypeAsync(string type);
    }
}

