using System.Collections.Concurrent;

namespace AIStory.API.Services
{
    /// <summary>Giới hạn số lần gọi gợi ý chương theo user để tránh 429 / lạm dụng.</summary>
    public interface IAISuggestRateLimitService
    {
        /// <summary>Kiểm tra và ghi nhận một lần gọi. Trả về true nếu được phép, false nếu vượt giới hạn.</summary>
        /// <param name="userId">ID user (tác giả).</param>
        /// <param name="retryAfterSeconds">Số giây nên chờ trước khi gọi lại (khi bị từ chối).</param>
        bool TryAcquire(Guid userId, out int retryAfterSeconds);
    }

    /// <summary>Rate limit in-memory theo user: tối đa N request trong cửa sổ W giây (sliding window).</summary>
    public class AISuggestRateLimitService : IAISuggestRateLimitService
    {
        private readonly int _maxRequests;
        private readonly int _windowSeconds;
        private readonly ConcurrentDictionary<Guid, object> _locks = new();
        private readonly ConcurrentDictionary<Guid, List<DateTime>> _requestsByUser = new();

        public AISuggestRateLimitService(IConfiguration configuration)
        {
            _maxRequests = configuration.GetValue("AI:RateLimitMaxRequests", 10);
            _windowSeconds = Math.Max(1, configuration.GetValue("AI:RateLimitWindowSeconds", 60));
        }

        public bool TryAcquire(Guid userId, out int retryAfterSeconds)
        {
            retryAfterSeconds = 0;
            var lockObj = _locks.GetOrAdd(userId, _ => new object());
            lock (lockObj)
            {
                var now = DateTime.UtcNow;
                var windowStart = now.AddSeconds(-_windowSeconds);
                var list = _requestsByUser.GetOrAdd(userId, _ => new List<DateTime>());

                list.RemoveAll(t => t < windowStart);

                if (list.Count >= _maxRequests)
                {
                    var oldestInWindow = list.Min();
                    retryAfterSeconds = (int)Math.Ceiling((oldestInWindow.AddSeconds(_windowSeconds) - now).TotalSeconds);
                    retryAfterSeconds = Math.Max(1, retryAfterSeconds);
                    return false;
                }

                list.Add(now);
                return true;
            }
        }
    }
}
