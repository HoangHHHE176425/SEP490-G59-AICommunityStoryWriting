using BusinessObjects.Entities;

public interface IStoryService
{
    story Create(
        string title,
        string summary,
        int categoryId,
        int authorId,            
        string? coverImageUrl,
        int expectedChapters,
        int releaseFrequencyDays,
        string ageRating
    );

    IEnumerable<story> GetAll();

    story? GetById(int id);

    IEnumerable<story> GetByAuthor(int authorId);

    bool Update(
        int id,
        string title,
        string? summary,
        int categoryId,
        string status
    );

    bool Delete(int id);
}
