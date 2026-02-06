using BusinessObjects.Entities;
using BusinessObjects.Models;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class OtpDAO
    {
        private static OtpDAO instance = null;
        private static readonly object instanceLock = new object();
        public static OtpDAO Instance
        {
            get { lock (instanceLock) { return instance ??= new OtpDAO(); } }
        }

        public async Task AddOtp(StoryPlatformDbContext context, OtpVerification otp)
        {
            context.OtpVerifications.Add(otp);
            await context.SaveChangesAsync();
        }

        public async Task<OtpVerification?> GetValidOtp(StoryPlatformDbContext context, Guid userId, string otpCode, string type)
        {
            return await context.OtpVerifications
                .Where(o => o.UserId == userId
                         && o.OtpCode == otpCode
                         && o.Type == type
                         && (o.IsUsed == false || o.IsUsed == null)
                         && o.ExpiredAt > DateTime.UtcNow)
                .OrderByDescending(o => o.CreatedAt)
                .FirstOrDefaultAsync();
        }

        public async Task MarkOtpAsUsed(StoryPlatformDbContext context, Guid otpId)
        {
            var otp = await context.OtpVerifications.FindAsync(otpId);
            if (otp != null)
            {
                otp.IsUsed = true;
                context.OtpVerifications.Update(otp);
                await context.SaveChangesAsync();
            }
        }
    }
}