namespace Services.Interfaces
{
    public interface IUserLookup
    {
        bool Exists(Guid userId);
        bool IsAuthorWritingSuspended(Guid authorUserId);
    }
}

