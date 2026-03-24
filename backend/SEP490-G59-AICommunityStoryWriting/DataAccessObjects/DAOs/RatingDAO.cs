using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class RatingDAO
    {
        public static ratings? GetByUserAndStory(Guid userId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            return context.ratings.AsNoTracking()
                .FirstOrDefault(r => r.user_id == userId && r.story_id == storyId);
        }

        /// Tạo mới rating (1 user chỉ 1 rating/story). Nếu đã tồn tại thì ném lỗi.
        public static void CreateOnce(Guid userId, Guid storyId, int starValue, string? reviewText, string status = "VISIBLE")
        {
            using var context = new StoryPlatformDbContext();
            var existing = context.ratings.FirstOrDefault(r => r.user_id == userId && r.story_id == storyId);
            if (existing == null)
            {
                var rating = new ratings
                {
                    id = Guid.NewGuid(),
                    user_id = userId,
                    story_id = storyId,
                    star_value = starValue,
                    review_text = reviewText,
                    status = status,
                    created_at = DateTime.Now
                };
                context.ratings.Add(rating);
            }
            else
            {
                throw new InvalidOperationException("Bạn đã đánh giá truyện này rồi và không thể đánh giá lại.");
            }
            context.SaveChanges();
        }

        public static (decimal avg, int count) GetAverageAndCount(Guid storyId, string status = "VISIBLE")
        {
            using var context = new StoryPlatformDbContext();
            var query = context.ratings.AsNoTracking()
                .Where(r => r.story_id == storyId && r.status == status && r.star_value.HasValue);
            var count = query.Count();
            if (count == 0) return (0m, 0);
            var sum = query.Sum(r => r.star_value!.Value);
            var avg = Math.Round((decimal)sum / count, 2, MidpointRounding.AwayFromZero);
            return (avg, count);
        }

        /// <summary>Lấy danh sách đánh giá của story (status = VISIBLE), có user display name. Sắp xếp mới nhất trước.</summary>
        public static IReadOnlyList<ratings> GetByStoryId(Guid storyId, string status = "VISIBLE")
        {
            using var context = new StoryPlatformDbContext();
            return context.ratings.AsNoTracking()
                .Include(r => r.user)
                .ThenInclude(u => u!.user_profiles)
                .Where(r => r.story_id == storyId && r.status == status && r.star_value.HasValue)
                .OrderByDescending(r => r.created_at)
                .ToList();
        }
    }
}

