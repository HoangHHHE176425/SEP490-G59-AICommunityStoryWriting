using BusinessObjects.Entities;

namespace Repositories.Interfaces
{
    public interface IAIUsageLogRepository
    {
        void Log(ai_usage_logs log);
    }
}
