using DataAccessObjects.DAOs;
using Services.Interfaces;

namespace Services.Implementations.Lookups
{
    public class UserLookup : IUserLookup
    {
        public bool Exists(Guid userId) => UserDAO.Exists(userId);

        public bool IsAuthor(Guid userId) => UserDAO.IsAuthor(userId);

        public bool IsAuthorWritingSuspended(Guid authorUserId) => UserDAO.IsAuthorWritingSuspended(authorUserId);

        public int CountAuthorsExcludingBanned() => UserDAO.CountAuthorsExcludingBanned();
    }
}

