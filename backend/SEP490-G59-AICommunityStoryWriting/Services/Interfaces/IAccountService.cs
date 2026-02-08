using Services.DTOs.Account;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interfaces
{
    public interface IAccountService
    {
        Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
        Task UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
        Task<UserProfileResponse> GetProfileAsync(Guid userId);
        Task DeleteAccountAsync(Guid userId);
    }
}
