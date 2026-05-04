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
        private readonly IUserRepository _userRepo;

        public AdminPolicyService(
            IPolicyRepository policyRepo,
            IAuthorPolicyAcceptanceRepository acceptRepo,
            IUserRepository userRepo)
        {
            _policyRepo = policyRepo;
            _acceptRepo = acceptRepo;
            _userRepo = userRepo;
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
            if (string.Equals(type, "DEFAULT", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Loại policy DEFAULT không còn được hỗ trợ. Chỉ dùng USER, AUTHOR hoặc AI.");
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

            await SyncAuthorResignFlagsAsync();

            return MapDetail(policy);
        }

        public async Task<bool> UpdateAsync(Guid id, AdminUpdatePolicyRequest request)
        {
            var policy = await _policyRepo.GetPolicyByIdAsync(id);
            if (policy == null) return false;

            var prevType = policy.type ?? string.Empty;
            var prevVersion = policy.version ?? string.Empty;
            var prevContent = policy.content ?? string.Empty;
            var prevRequireResign = policy.require_resign ?? false;
            var type = request.Type.Trim().ToUpperInvariant();
            if (string.Equals(type, "DEFAULT", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Loại policy DEFAULT không còn được hỗ trợ. Chọn USER, AUTHOR hoặc AI.");
            policy.type = type;
            policy.version = request.Version.Trim();
            policy.content = request.Content;
            policy.require_resign = request.RequireResign;
            var policyContentChanged =
                !string.Equals(prevType, policy.type, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(prevVersion, policy.version, StringComparison.Ordinal) ||
                !string.Equals(prevContent, policy.content, StringComparison.Ordinal) ||
                prevRequireResign != (policy.require_resign ?? false);

            var wasActive = policy.is_active == true;
            policy.is_active = request.IsActive;
            if (!wasActive && policy.is_active == true)
            {
                // Reactivate cùng policy: giữ mốc active cũ để không ép re-sign lại nếu không có revision mới.
                // Policy mới (chưa từng active) hoặc có revision thay đổi thì tạo mốc active mới.
                if (!policy.activated_at.HasValue || policyContentChanged)
                {
                    policy.activated_at = DateTime.UtcNow;
                }
            }
            if (wasActive && policy.is_active != true)
            {
                // keep activated_at as historical value
            }
            var policyContentChangedWhileActive =
                wasActive &&
                policy.is_active == true &&
                policyContentChanged;
            if (policyContentChangedWhileActive)
            {
                // Bump effective time so old acceptance records are treated as stale.
                policy.activated_at = DateTime.UtcNow;
            }

            await _policyRepo.UpdatePolicyAsync(policy);

            if (policy.is_active == true)
            {
                await _policyRepo.DeactivateOtherPoliciesOfTypeAsync(type, policy.id);
            }

            await SyncAuthorResignFlagsAsync();

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

            if (string.Equals(policy.type, "DEFAULT", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Loại DEFAULT đã bỏ; không thể kích hoạt policy này. Đổi loại hoặc xóa bản ghi.");

            policy.is_active = true;
            // Không reset activated_at khi bật lại cùng policy đã từng active,
            // tránh ép tác giả ký lại nếu policy không đổi revision.
            policy.activated_at ??= DateTime.UtcNow;
            await _policyRepo.UpdatePolicyAsync(policy);

            if (!string.IsNullOrWhiteSpace(policy.type))
            {
                await _policyRepo.DeactivateOtherPoliciesOfTypeAsync(policy.type, policy.id);
            }

            await SyncAuthorResignFlagsAsync();

            return true;
        }

        public async Task<bool> DeactivateAsync(Guid id)
        {
            var policy = await _policyRepo.GetPolicyByIdAsync(id);
            if (policy == null) return false;

            policy.is_active = false;
            await _policyRepo.UpdatePolicyAsync(policy);

            await SyncAuthorResignFlagsAsync();
            return true;
        }

        private async Task SyncAuthorResignFlagsAsync()
        {
            var activeAuthorPolicy = await _policyRepo.GetActivePolicyByTypeAsync("AUTHOR");
            if (activeAuthorPolicy == null)
            {
                await _userRepo.ClearAuthorMustResignPolicyFlagAsync();
                return;
            }

            if (activeAuthorPolicy.require_resign == true)
            {
                await _userRepo.MarkAuthorsMustResignPolicyAsync(activeAuthorPolicy.id);
                return;
            }

            await _userRepo.ClearAuthorMustResignPolicyFlagAsync();
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

