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
        Task ChangePasswordAsync(int userId, ChangePasswordRequest request);
        Task UpdateProfileAsync(int userId, UpdateProfileRequest request);
        Task<UserProfileResponse> GetProfileAsync(int userId);
    }
}
