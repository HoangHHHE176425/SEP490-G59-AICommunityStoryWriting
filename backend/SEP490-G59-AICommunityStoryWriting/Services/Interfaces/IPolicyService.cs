using Services.DTOs.Policies;

namespace Services.Interfaces
{
    public interface IPolicyService
    {
        Task<PolicyResponseDto?> GetActivePolicyAsync(string type);
        Task<AuthorPolicyStatusDto?> GetMyAuthorPolicyStatusAsync(Guid userId, string type);
        Task<bool> AcceptPolicyAsAuthorAsync(Guid userId, Guid policyId, string? ipAddress, string? userAgent);
    }
}

