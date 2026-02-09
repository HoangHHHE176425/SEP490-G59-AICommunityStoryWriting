using BusinessObjects.Entities;
using Repositories.Interfaces;
using Services.DTOs.Policies;
using Services.Interfaces;

namespace Services.Implementations
{
    public class PolicyService : IPolicyService
    {
        private readonly IPolicyRepository _policyRepo;
        private readonly IAuthorPolicyAcceptanceRepository _acceptRepo;

        public PolicyService(IPolicyRepository policyRepo, IAuthorPolicyAcceptanceRepository acceptRepo)
        {
            _policyRepo = policyRepo;
            _acceptRepo = acceptRepo;
        }

        public async Task<PolicyResponseDto?> GetActivePolicyAsync(string type)
        {
            var policy = await _policyRepo.GetActivePolicyByTypeAsync(type);
            return policy == null ? null : Map(policy);
        }

        public async Task<AuthorPolicyStatusDto?> GetMyAuthorPolicyStatusAsync(Guid userId, string type)
        {
            var active = await _policyRepo.GetActivePolicyByTypeAsync(type);
            if (active == null) return null;

            var acceptance = await _acceptRepo.GetAcceptanceAsync(userId, active.id);

            return new AuthorPolicyStatusDto
            {
                Policy = Map(active),
                HasAccepted = acceptance != null,
                AcceptedAt = acceptance?.accepted_at
            };
        }

        public async Task<bool> AcceptPolicyAsAuthorAsync(Guid userId, Guid policyId, string? ipAddress, string? userAgent)
        {
            var policy = await _policyRepo.GetPolicyByIdAsync(policyId);
            if (policy == null) throw new Exception("Policy not found.");
            if (policy.is_active != true) throw new Exception("Policy is not active.");

            var existing = await _acceptRepo.GetAcceptanceAsync(userId, policyId);
            if (existing != null) return false; // already accepted

            var row = new author_policy_acceptances
            {
                id = Guid.NewGuid(),
                user_id = userId,
                policy_id = policyId,
                accepted_at = DateTime.UtcNow,
                ip_address = ipAddress,
                user_agent = userAgent,
                accepted_for = "AUTHOR"
            };

            await _acceptRepo.AddAcceptanceAsync(row);
            return true;
        }

        private static PolicyResponseDto Map(system_policies p)
        {
            return new PolicyResponseDto
            {
                Id = p.id,
                Type = p.type,
                Version = p.version,
                Content = p.content,
                IsActive = p.is_active ?? false,
                RequireResign = p.require_resign ?? false,
                CreatedAt = p.created_at,
                ActivatedAt = p.activated_at
            };
        }
    }
}

