using BusinessObjects.Entities;
using Repositories;

namespace Services.Implementations
{
    public class StoryService : IStoryService
    {
        private readonly IStoryRepository _storyRepository;

        public StoryService(IStoryRepository storyRepository)
        {
            _storyRepository = storyRepository;
        }

        public story Create(
        string title,
        string summary,
        int categoryId,
        int authorId,
        string? coverImageUrl,
        int expectedChapters,
        int releaseFrequencyDays,
        string ageRating
)
        {
            var story = new story
            {
                title = title,
                slug = GenerateSlug(title),
                summary = summary,
                category_id = categoryId,
                author_id = authorId,
                cover_image = coverImageUrl,

                status = "DRAFT",
                sale_type = "CHAPTER",
                access_type = "FREE",

                expected_chapters = expectedChapters,
                release_frequency_days = releaseFrequencyDays,
                age_rating = ageRating,

                total_chapters = 0,
                total_views = 0,
                total_favorites = 0,
                avg_rating = 0,
                word_count = 0,

                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            _storyRepository.Add(story);
            return story;
        }

        public IEnumerable<story> GetAll()
            => _storyRepository.GetAll().ToList();

        public story? GetById(int id)
            => _storyRepository.GetById(id);

        public IEnumerable<story> GetByAuthor(int authorId)
            => _storyRepository
                .GetAll()
                .Where(s => s.author_id == authorId)
                .ToList();

        public bool Update(
            int id,
            string title,
            string? summary,
            int categoryId,
            string status
        )
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            story.title = title;
            story.slug = GenerateSlug(title);
            story.summary = summary;
            story.category_id = categoryId;
            story.status = status;
            story.updated_at = DateTime.Now;

            _storyRepository.Update(story);
            return true;
        }

        public bool Delete(int id)
        {
            var story = _storyRepository.GetById(id);
            if (story == null)
                return false;

            _storyRepository.Delete(id);
            return true;
        }

        private string GenerateSlug(string title)
        {
            return title
                .ToLower()
                .Trim()
                .Replace(" ", "-");
        }
    }
}
