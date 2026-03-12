using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace AIStory.API.Services
{
    /// <summary>Giới hạn số lần gọi AI theo user (mặc định 3 lần/ngày, admin chỉnh qua API).</summary>
    public interface IAISuggestRateLimitService
    {
        /// <summary>Kiểm tra và ghi nhận một lần gọi. Trả về true nếu được phép, false nếu vượt giới hạn.</summary>
        /// <param name="userId">ID user (tác giả).</param>
        /// <param name="retryAfterSeconds">Số giây nên chờ trước khi gọi lại (khi bị từ chối).</param>
        bool TryAcquire(Guid userId, out int retryAfterSeconds);

        /// <summary>Thông tin giới hạn sử dụng AI trong 24h (rolling): limit, đã dùng, còn lại, thời điểm reset.</summary>
        AIUsageLimitInfo GetDailyLimitInfo(Guid userId);
    }

    /// <summary>Thông tin giới hạn AI (rolling 24h).</summary>
    public class AIUsageLimitInfo
    {
        public int LimitPerDay { get; set; }
        public int UsedInWindow { get; set; }
        public int Remaining { get; set; }
        public DateTime? ResetsAtUtc { get; set; }
    }

    /// <summary>Rate limit in-memory: N request / 24h (rolling) theo user. N đọc từ ai_configs qua IAIUsageLimitConfigService, admin chỉnh qua API.</summary>
    public class AISuggestRateLimitService : IAISuggestRateLimitService
    {
        private const int SecondsPerDay = 86400;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConcurrentDictionary<Guid, object> _locks = new();
        private readonly ConcurrentDictionary<Guid, List<DateTime>> _requestsByUser = new();

        public AISuggestRateLimitService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        private int GetMaxRequestsPerDay()
        {
            using var scope = _scopeFactory.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IAIUsageLimitConfigService>().GetMaxRequestsPerDay();
        }

        public bool TryAcquire(Guid userId, out int retryAfterSeconds)
        {
            retryAfterSeconds = 0;
            var maxRequests = GetMaxRequestsPerDay();
            var lockObj = _locks.GetOrAdd(userId, _ => new object());
            lock (lockObj)
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddSeconds(-SecondsPerDay);
                var list = _requestsByUser.GetOrAdd(userId, _ => new List<DateTime>());

                list.RemoveAll(t => t < windowStart);

                if (list.Count >= maxRequests)
                {
                    var oldestInWindow = list.Min();
                    retryAfterSeconds = (int)Math.Ceiling((oldestInWindow.AddSeconds(SecondsPerDay) - now).TotalSeconds);
                    retryAfterSeconds = Math.Max(1, retryAfterSeconds);
                    return false;
                }

                list.Add(now);
                return true;
            }
        }

        public AIUsageLimitInfo GetDailyLimitInfo(Guid userId)
        {
            var maxRequests = GetMaxRequestsPerDay();
            var lockObj = _locks.GetOrAdd(userId, _ => new object());
            lock (lockObj)
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddSeconds(-SecondsPerDay);
                var list = _requestsByUser.GetOrAdd(userId, _ => new List<DateTime>());
                list.RemoveAll(t => t < windowStart);
                var used = list.Count;
                DateTime? resetsAt = null;
                if (list.Count > 0)
                    resetsAt = list.Min().AddSeconds(SecondsPerDay);
                return new AIUsageLimitInfo
                {
                    LimitPerDay = maxRequests,
                    UsedInWindow = used,
                    Remaining = Math.Max(0, maxRequests - used),
                    ResetsAtUtc = resetsAt
                };
            }
        }
    }
}
