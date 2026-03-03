using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class AIUsageLogRepository : IAIUsageLogRepository
    {
        public void Log(ai_usage_logs log)
        {
            AIUsageLogDAO.Add(log);
        }
    }
}
