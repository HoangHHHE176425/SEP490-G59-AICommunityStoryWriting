using BusinessObjects.Entities;
using DataAccessObjects.DAOs;

namespace Repositories
{
    public class StoryRepository : IStoryRepository
    {
        public IQueryable<stories> GetAll()
            => StoryDAO.GetAll();

        public stories? GetById(Guid id)
            => StoryDAO.GetById(id);

        public stories? GetBySlug(string slug)
            => StoryDAO.GetBySlug(slug);

        public void Add(stories story)
            => StoryDAO.Add(story);

        public void Add(stories story, IEnumerable<Guid> categoryIds)
            => StoryDAO.AddWithCategories(story, categoryIds);

        public void Update(stories story)
            => StoryDAO.Update(story);

        public void Delete(Guid id)
            => StoryDAO.Delete(id);
    }
}
