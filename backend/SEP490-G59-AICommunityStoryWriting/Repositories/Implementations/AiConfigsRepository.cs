using DataAccessObjects.DAOs;

namespace Repositories;

public class AiConfigsRepository : IAiConfigsRepository
{
    public string? GetValue(string key) => AiConfigsDAO.GetValue(key);

    public void Upsert(string key, string value, string? description = null) => AiConfigsDAO.Upsert(key, value, description);
}
