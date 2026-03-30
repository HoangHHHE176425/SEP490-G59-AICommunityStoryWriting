using DataAccessObjects.DAOs;
using Services.Interfaces;

namespace Services.Implementations.Lookups;

public class UserActivityLookup : IUserActivityLookup
{
    public bool HasReadAnyChapterOfStory(Guid userId, Guid storyId) =>
        UserActivityLogDAO.HasReadAnyChapterOfStory(userId, storyId);
}
