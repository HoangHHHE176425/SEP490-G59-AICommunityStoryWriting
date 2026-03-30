using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Services.Interfaces;

namespace Services.Implementations.Lookups
{
    public class StoryLookup : IStoryLookup
    {
        public stories? GetById(Guid storyId) => StoryDAO.GetById(storyId);

        public void Update(stories story) => StoryDAO.Update(story);
    }
}

