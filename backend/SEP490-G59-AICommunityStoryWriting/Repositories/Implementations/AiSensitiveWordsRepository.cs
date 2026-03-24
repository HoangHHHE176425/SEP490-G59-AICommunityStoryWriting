using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories;

public class AiSensitiveWordsRepository : IAiSensitiveWordsRepository
{
    public IReadOnlyList<ai_sensitive_words> GetAll(string? category = null)
        => AiSensitiveWordsDAO.GetAll(category);

    public ai_sensitive_words? GetById(Guid id)
        => AiSensitiveWordsDAO.GetById(id);

    public void Add(ai_sensitive_words entity)
        => AiSensitiveWordsDAO.Add(entity);

    public bool Delete(Guid id)
        => AiSensitiveWordsDAO.Delete(id);
}
