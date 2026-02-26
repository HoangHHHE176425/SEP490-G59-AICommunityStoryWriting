using BusinessObjects;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class ModeratorCategoryAssignmentRepository : IModeratorCategoryAssignmentRepository
    {
        private readonly StoryPlatformDbContext _context;

        public ModeratorCategoryAssignmentRepository(StoryPlatformDbContext context)
        {
            _context = context;
        }

        public Task<List<Guid>> GetCategoryIdsAsync(Guid moderatorId)
            => ModeratorCategoryAssignmentDAO.Instance.GetCategoryIdsAsync(_context, moderatorId);

        public Task ReplaceAssignmentsAsync(Guid moderatorId, IReadOnlyCollection<Guid> categoryIds)
            => ModeratorCategoryAssignmentDAO.Instance.ReplaceAssignmentsAsync(_context, moderatorId, categoryIds);
    }
}

