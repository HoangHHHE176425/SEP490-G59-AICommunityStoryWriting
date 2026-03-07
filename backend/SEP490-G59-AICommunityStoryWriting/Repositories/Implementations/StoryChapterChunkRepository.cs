using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations
{
    public class StoryChapterChunkRepository : IStoryChapterChunkRepository
    {
        public List<story_chapter_chunks> GetByStoryId(Guid storyId)
            => StoryChapterChunkDAO.GetByStoryId(storyId);

        public List<story_chapter_chunks> GetByStoryIdWithEmbeddings(Guid storyId)
            => StoryChapterChunkDAO.GetByStoryIdWithEmbeddings(storyId);

        public List<story_chapter_chunks> GetChunksByIds(IEnumerable<Guid> ids)
            => StoryChapterChunkDAO.GetChunksByIds(ids);

        public int CountByStoryId(Guid storyId)
            => StoryChapterChunkDAO.CountByStoryId(storyId);

        public void DeleteByStoryId(Guid storyId)
            => StoryChapterChunkDAO.DeleteByStoryId(storyId);

        public void DeleteByChapterId(Guid chapterId)
            => StoryChapterChunkDAO.DeleteByChapterId(chapterId);

        public void AddRange(IEnumerable<story_chapter_chunks> chunks)
            => StoryChapterChunkDAO.AddRange(chunks);
    }
}
