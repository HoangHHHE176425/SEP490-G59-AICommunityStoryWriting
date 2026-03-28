using BusinessObjects.Entities;

namespace Services.Interfaces
{
    public interface IStoryLookup
    {
        stories? GetById(Guid storyId);
        void Update(stories story);
    }
}

