using BusinessObjects.Entities;
using BusinessObjects.Models;
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

        public Task AddOtp(OtpVerification otp)
            => OtpDAO.Instance.AddOtp(_context, otp);

        public Task<OtpVerification?> GetValidOtp(Guid userId, string otpCode, string type)
            => OtpDAO.Instance.GetValidOtp(_context, userId, otpCode, type);

        public Task MarkOtpAsUsed(Guid otpId)
            => OtpDAO.Instance.MarkOtpAsUsed(_context, otpId);
    }
}
