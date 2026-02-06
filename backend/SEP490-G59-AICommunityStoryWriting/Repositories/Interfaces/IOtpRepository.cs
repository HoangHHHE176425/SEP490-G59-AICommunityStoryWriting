using BusinessObjects.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Interfaces
{
    public interface IOtpRepository
    {
        Task AddOtp(OtpVerification otp);

        Task<OtpVerification?> GetValidOtp(Guid userId, string otpCode, string type);
        Task MarkOtpAsUsed(Guid otpId);
    }
}
