using BusinessObjects.Entities;
using System.Collections.Generic;
using DataAccessObjects.Queries;
using Repositories.Interfaces;
using Services.DTOs.Admin;
using Services.DTOs.Admin.Policies;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AdminPolicyService : IAdminPolicyService
    {
        private readonly IPolicyRepository _policyRepo;
        private readonly IAuthorPolicyAcceptanceRepository _acceptRepo;

        public AdminPolicyService(IPolicyRepository policyRepo, IAuthorPolicyAcceptanceRepository acceptRepo)
        {
            _policyRepo = policyRepo;
            _acceptRepo = acceptRepo;
        }

        public async Task<PagedResultDto<AdminPolicyListItemDto>> GetPoliciesAsync(AdminPolicyQueryDto query)
        {
            var daoQuery = new AdminPolicyQuery
            {
                Type = query.Type,
                IsActive = query.IsActive,
                Search = query.Search,
                Page = query.Page,
                PageSize = query.PageSize,
                SortBy = query.SortBy,
                SortOrder = query.SortOrder
            };

            var (items, total) = await _policyRepo.GetPoliciesAsync(daoQuery);
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

            return new PagedResultDto<AdminPolicyListItemDto>
            {
                Items = items.Select(MapListItem),
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public async Task<AdminPolicyStatsDto> GetStatsAsync()
        {
            var (total, active, byType) = await _policyRepo.GetStatsAsync();
            return new AdminPolicyStatsDto
            {
                Total = total,
                Active = active,
                ByType = byType ?? new Dictionary<string, int>()
            };
        }

        public async Task<AdminPolicyDetailDto?> GetPolicyByIdAsync(Guid id)
        {
            var p = await _policyRepo.GetPolicyByIdAsync(id);
            return p == null ? null : MapDetail(p);
        }

        public async Task<AdminPolicyDetailDto> CreateAsync(AdminCreatePolicyRequest request)
        {
            var type = request.Type.Trim().ToUpperInvariant();
            var version = request.Version.Trim();

            var policy = new system_policies
            {
                id = Guid.NewGuid(),
                type = type,
                version = version,
                content = request.Content,
                is_active = request.IsActive,
                require_resign = request.RequireResign,
                created_at = DateTime.UtcNow,
                activated_at = request.IsActive ? DateTime.UtcNow : null
            };

            await _policyRepo.AddPolicyAsync(policy);

            if (policy.is_active == true)
            {
                await _policyRepo.DeactivateOtherPoliciesOfTypeAsync(type, policy.id);
            }

            return MapDetail(policy);
        }

        public async Task<bool> UpdateAsync(Guid id, AdminUpdatePolicyRequest request)
        {
            var policy = await _policyRepo.GetPolicyByIdAsync(id);
            if (policy == null) return false;

            var type = request.Type.Trim().ToUpperInvariant();
            policy.type = type;
            policy.version = request.Version.Trim();
            policy.content = request.Content;
            policy.require_resign = request.RequireResign;

            var wasActive = policy.is_active == true;
            policy.is_active = request.IsActive;
            if (!wasActive && policy.is_active == true)
            {
                policy.activated_at = DateTime.UtcNow;
            }
            if (wasActive && policy.is_active != true)
            {
                // keep activated_at as historical value
            }

            await _policyRepo.UpdatePolicyAsync(policy);

            if (policy.is_active == true)
            {
                await _policyRepo.DeactivateOtherPoliciesOfTypeAsync(type, policy.id);
            }

            return true;
        }

        public async Task<bool> DeleteAsync(Guid id)
        {
            var policy = await _policyRepo.GetPolicyByIdAsync(id);
            if (policy == null) return false;

            var usedCount = await _acceptRepo.CountByPolicyAsync(id);
            if (usedCount > 0)
            {
                throw new InvalidOperationException("Cannot delete policy that has acceptance records. Deactivate it instead.");
            }

            await _policyRepo.DeletePolicyAsync(policy);
            return true;
        }

        public async Task<bool> ActivateAsync(Guid id)
        {
            var policy = await _policyRepo.GetPolicyByIdAsync(id);
            if (policy == null) return false;

            policy.is_active = true;
            policy.activated_at = DateTime.UtcNow;
            await _policyRepo.UpdatePolicyAsync(policy);

            if (!string.IsNullOrWhiteSpace(policy.type))
            {
                await _policyRepo.DeactivateOtherPoliciesOfTypeAsync(policy.type, policy.id);
            }

            return true;
        }

        public async Task<bool> DeactivateAsync(Guid id)
        {
            var policy = await _policyRepo.GetPolicyByIdAsync(id);
            if (policy == null) return false;

            policy.is_active = false;
            await _policyRepo.UpdatePolicyAsync(policy);
            return true;
        }

        private static AdminPolicyListItemDto MapListItem(system_policies p)
        {
            return new AdminPolicyListItemDto
            {
                Id = p.id,
                Type = p.type,
                Version = p.version,
                IsActive = p.is_active ?? false,
                RequireResign = p.require_resign ?? false,
                CreatedAt = p.created_at,
                ActivatedAt = p.activated_at
            };
        }

        private static AdminPolicyDetailDto MapDetail(system_policies p)
        {
            return new AdminPolicyDetailDto
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

