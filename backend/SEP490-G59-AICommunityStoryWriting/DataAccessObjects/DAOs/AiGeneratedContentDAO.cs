using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

/// <summary>Lấy và thêm bản ghi ai_generated_content.</summary>
public static class AiGeneratedContentDAO
{
    /// <summary>Bản AI sinh ra gần nhất cho chương (created_at desc).</summary>
    public static ai_generated_content? GetLatestByChapterId(Guid chapterId)
    {
        using var context = new StoryPlatformDbContext();
        return context.ai_generated_content
            .AsNoTracking()
            .Where(a => a.chapter_id == chapterId && a.ai_output != null && a.ai_output.Length > 0)
            .OrderByDescending(a => a.created_at)
            .FirstOrDefault();
    }

    /// <summary>Tất cả bản AI của chương (mới nhất trước), giới hạn số lượng để so sánh với từng bản rồi lấy điểm cao nhất.</summary>
    public static List<ai_generated_content> GetAllByChapterId(Guid chapterId, int maxCount = 50)
    {
        using var context = new StoryPlatformDbContext();
        return context.ai_generated_content
            .AsNoTracking()
            .Where(a => a.chapter_id == chapterId && a.ai_output != null && a.ai_output.Length > 0)
            .OrderByDescending(a => a.created_at)
            .Take(maxCount)
            .ToList();
    }

    /// <summary>Tất cả bản AI theo draft chapter id (khi chapter thật chưa được tạo).</summary>
    public static List<ai_generated_content> GetAllByDraftChapterId(Guid draftChapterId, int maxCount = 50)
    {
        using var context = new StoryPlatformDbContext();
        return context.ai_generated_content
            .AsNoTracking()
            .Where(a => a.draft_chapter_id == draftChapterId && a.ai_output != null && a.ai_output.Length > 0)
            .OrderByDescending(a => a.created_at)
            .Take(maxCount)
            .ToList();
    }

    /// <summary>Bản AI theo truyện + thứ tự chương (chapter_index khớp order_index của chương).</summary>
    public static List<ai_generated_content> GetAllByStoryIdAndChapterIndex(Guid storyId, int chapterIndex, int maxCount = 50)
    {
        using var context = new StoryPlatformDbContext();
        return context.ai_generated_content
            .AsNoTracking()
            .Where(a => a.story_id == storyId && a.chapter_index == chapterIndex && a.ai_output != null && a.ai_output.Length > 0)
            .OrderByDescending(a => a.created_at)
            .Take(maxCount)
            .ToList();
    }

    /// <summary>Lấy theo id.</summary>
    public static ai_generated_content? GetById(Guid id)
    {
        using var context = new StoryPlatformDbContext();
        return context.ai_generated_content.AsNoTracking().FirstOrDefault(a => a.id == id);
    }

    /// <summary>Thêm bản ghi (dùng khi lưu nội dung AI từ co-create).</summary>
    public static void Add(ai_generated_content entity)
    {
        using var context = new StoryPlatformDbContext();
        context.ai_generated_content.Add(entity);
        context.SaveChanges();
    }

    /// <summary>Gán chương và đồng bộ chapter_index với order_index của chương vừa tạo.</summary>
    public static void UpdateChapterId(Guid id, Guid chapterId, int chapterOrderIndex)
    {
        using var context = new StoryPlatformDbContext();
        var row = context.ai_generated_content.FirstOrDefault(a => a.id == id);
        if (row != null)
        {
            row.chapter_id = chapterId;
            row.chapter_index = chapterOrderIndex;
            context.SaveChanges();
        }
    }

    /// <summary>Map toàn bộ bản ghi draft sang chapter thật sau khi tạo chương.</summary>
    public static void BindDraftChapterId(Guid draftChapterId, Guid chapterId, int chapterOrderIndex)
    {
        using var context = new StoryPlatformDbContext();
        var rows = context.ai_generated_content.Where(a => a.draft_chapter_id == draftChapterId).ToList();
        if (rows.Count == 0) return;
        foreach (var row in rows)
        {
            row.chapter_id = chapterId;
            row.chapter_index = chapterOrderIndex;
        }
        context.SaveChanges();
    }

    /// <summary>Xóa mọi bản ghi AI gắn chapter (đồng bộ khi tác giả xóa chương DRAFT).</summary>
    /// <remarks>
    /// Dùng SQL trực tiếp để không materialize entity: DB cũ chưa chạy script thêm <c>chapter_index</c>
    /// vẫn xóa được (tránh lỗi "Invalid column name 'chapter_index'").
    /// </remarks>
    public static void DeleteAllByChapterId(Guid chapterId)
    {
        using var context = new StoryPlatformDbContext();
        context.Database.ExecuteSqlInterpolated(
            $"DELETE FROM ai_generated_content WHERE chapter_id = {chapterId}");
    }
}
