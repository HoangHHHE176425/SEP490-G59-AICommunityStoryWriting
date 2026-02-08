using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Repositories.Implementations
{
    public class OtpRepository : IOtpRepository
    {
        private readonly StoryPlatformDbContext _context;

        public OtpRepository(StoryPlatformDbContext context)
        {
            _context = context;
        }

        public Task AddOtp(otp_verifications otp)
            => OtpDAO.Instance.AddOtp(_context, otp);

        public Task<otp_verifications?> GetValidOtp(Guid userId, string otpCode, string type)
            => OtpDAO.Instance.GetValidOtp(_context, userId, otpCode, type);

        public Task MarkOtpAsUsed(Guid otpId)
            => OtpDAO.Instance.MarkOtpAsUsed(_context, otpId);
    }
}
