using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;

namespace AIStory.API.Services
{
    /// <summary>Giới hạn số lần gọi AI theo user và loại API (suggest vs co-create tách biệt). N đọc từ ai_configs.</summary>
    public interface IAISuggestRateLimitService
    {
        bool TryAcquire(Guid userId, AiRateLimitKind kind, out int retryAfterSeconds);
        AIUsageLimitInfo GetDailyLimitInfo(Guid userId, AiRateLimitKind kind);
    }

    /// <summary>Thông tin giới hạn AI (rolling 24h).</summary>
    public class AIUsageLimitInfo
    {
        public int LimitPerDay { get; set; }
        public int UsedInWindow { get; set; }
        public int Remaining { get; set; }
        public DateTime? ResetsAtUtc { get; set; }
    }

    /// <summary>Rate limit in-memory: N request / 24h (rolling) theo user và loại API.</summary>
    public class AISuggestRateLimitService : IAISuggestRateLimitService
    {
        private const int SecondsPerDay = 86400;

        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ConcurrentDictionary<string, object> _locks = new();
        /// <summary>Key: "{kind}:{userId}" → timestamps trong cửa sổ 24h.</summary>
        private readonly ConcurrentDictionary<string, List<DateTime>> _requestsByUserAndKind = new();

        public AISuggestRateLimitService(IServiceScopeFactory scopeFactory)
        {
            _scopeFactory = scopeFactory;
        }

        private static string StorageKey(AiRateLimitKind kind, Guid userId)
            => $"{(int)kind}:{userId:N}";

        private int GetMaxRequestsPerDay(AiRateLimitKind kind)
        {
            using var scope = _scopeFactory.CreateScope();
            return scope.ServiceProvider.GetRequiredService<IAIUsageLimitConfigService>().GetMaxRequestsPerDay(kind);
        }

        public bool TryAcquire(Guid userId, AiRateLimitKind kind, out int retryAfterSeconds)
        {
            retryAfterSeconds = 0;
            var maxRequests = GetMaxRequestsPerDay(kind);
            var sKey = StorageKey(kind, userId);
            var lockObj = _locks.GetOrAdd(sKey, _ => new object());
            lock (lockObj)
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddSeconds(-SecondsPerDay);
                var list = _requestsByUserAndKind.GetOrAdd(sKey, _ => new List<DateTime>());

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

        public AIUsageLimitInfo GetDailyLimitInfo(Guid userId, AiRateLimitKind kind)
        {
            var maxRequests = GetMaxRequestsPerDay(kind);
            var sKey = StorageKey(kind, userId);
            var lockObj = _locks.GetOrAdd(sKey, _ => new object());
            lock (lockObj)
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddSeconds(-SecondsPerDay);
                var list = _requestsByUserAndKind.GetOrAdd(sKey, _ => new List<DateTime>());
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
