using BusinessObjects;
using BusinessObjects.Entities;
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

        public async Task AddOtp(StoryPlatformDbContext context, otp_verifications otp)
        {
            context.otp_verifications.Add(otp);
            await context.SaveChangesAsync();
        }

        public async Task<otp_verifications?> GetValidOtp(StoryPlatformDbContext context, Guid userId, string otpCode, string type)
        {
            return await context.otp_verifications
                .Where(o => o.user_id == userId
                         && o.otp_code == otpCode
                         && o.type == type
                         && (o.is_used == false || o.is_used == null)
                         && o.expired_at > DateTime.UtcNow)
                .OrderByDescending(o => o.created_at)
                .FirstOrDefaultAsync();
        }

        public async Task MarkOtpAsUsed(StoryPlatformDbContext context, Guid otpId)
        {
            var otp = await context.otp_verifications.FindAsync(otpId);
            if (otp != null)
            {
                otp.is_used = true;
                context.otp_verifications.Update(otp);
                await context.SaveChangesAsync();
            }
        }
    }
}