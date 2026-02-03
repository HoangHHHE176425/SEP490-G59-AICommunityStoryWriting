using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories
{
    public class StoryRepository : IStoryRepository
    {
        public IQueryable<story> GetAll()
            => StoryDAO.GetAll();

        public story? GetById(int id)
            => StoryDAO.GetById(id);

        public story? GetBySlug(string slug)
            => StoryDAO.GetBySlug(slug);

        public void Add(story story)
            => StoryDAO.Add(story);

        public void Update(story story)
            => StoryDAO.Update(story);

        public void Delete(int id)
            => StoryDAO.Delete(id);
    }
}
