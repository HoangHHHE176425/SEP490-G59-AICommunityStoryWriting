namespace Repositories.Interfaces
{
    public interface IModeratorCategoryAssignmentRepository
    {
        Task<List<Guid>> GetCategoryIdsAsync(Guid moderatorId);
        Task ReplaceAssignmentsAsync(Guid moderatorId, IReadOnlyCollection<Guid> categoryIds);
    }
}

