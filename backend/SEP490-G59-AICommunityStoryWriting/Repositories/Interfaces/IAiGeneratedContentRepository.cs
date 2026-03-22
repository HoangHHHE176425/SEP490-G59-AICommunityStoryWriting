using BusinessObjects.Entities;

namespace Repositories;

/// <summary>Đọc và ghi bản ghi ai_generated_content (nội dung AI sinh ra theo chương).</summary>
public interface IAiGeneratedContentRepository
{
    /// <summary>Bản AI sinh ra gần nhất cho chương.</summary>
    ai_generated_content? GetLatestByChapterId(Guid chapterId);

    /// <summary>Tất cả bản AI của chương (để so sánh với từng bản, lấy điểm cao nhất).</summary>
    IReadOnlyList<ai_generated_content> GetAllByChapterId(Guid chapterId);

    /// <summary>Các bản AI của truyện tại một thứ tự chương (<c>chapter_index</c> = <c>order_index</c>), mới nhất trước.</summary>
    IReadOnlyList<ai_generated_content> GetAllByStoryIdAndChapterIndex(Guid storyId, int chapterIndex, int maxCount = 50);

    ai_generated_content? GetById(Guid id);

    /// <summary>Lưu bản nội dung AI (vd. từ co-create).</summary>
    void Add(ai_generated_content entity);

    /// <summary>Gán chapter_id (và đồng bộ chapter_index với thứ tự chương) khi tác giả tạo chương từ bản AI.</summary>
    void UpdateChapterId(Guid id, Guid chapterId, int chapterOrderIndex);
}
