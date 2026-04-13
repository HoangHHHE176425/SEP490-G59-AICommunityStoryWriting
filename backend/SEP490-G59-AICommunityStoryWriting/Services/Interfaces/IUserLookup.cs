namespace Services.Interfaces
{
    public interface IUserLookup
    {
        bool Exists(Guid userId);
        bool IsAuthorWritingSuspended(Guid authorUserId);
        /// <summary>Role AUTHOR, trừ status BANNED (không phụ thuộc truyện đã publish).</summary>
        int CountAuthorsExcludingBanned();
    }
}

