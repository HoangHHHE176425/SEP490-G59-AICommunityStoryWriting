using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories;

public class AiGeneratedContentRepository : IAiGeneratedContentRepository
{
    public ai_generated_content? GetLatestByChapterId(Guid chapterId)
        => AiGeneratedContentDAO.GetLatestByChapterId(chapterId);

    public IReadOnlyList<ai_generated_content> GetAllByChapterId(Guid chapterId)
        => AiGeneratedContentDAO.GetAllByChapterId(chapterId);

    public ai_generated_content? GetById(Guid id)
        => AiGeneratedContentDAO.GetById(id);

    public void Add(ai_generated_content entity)
        => AiGeneratedContentDAO.Add(entity);

    public void UpdateChapterId(Guid id, Guid chapterId)
        => AiGeneratedContentDAO.UpdateChapterId(id, chapterId);
}
