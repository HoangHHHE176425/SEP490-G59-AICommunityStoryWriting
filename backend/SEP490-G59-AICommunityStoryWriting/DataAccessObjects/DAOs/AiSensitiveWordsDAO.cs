using BusinessObjects;
using BusinessObjects.Entities;

namespace DataAccessObjects.DAOs;

/// <summary>Từ cấm / từ nhạy cảm dùng cho check-chapter (guardrail). Lưu trong ai_sensitive_words, category = BannedWord.</summary>
public static class AiSensitiveWordsDAO
{
    /// <summary>Lấy tất cả từ (có thể lọc theo category). Category "BannedWord" = từ cấm cho check-chapter.</summary>
    public static List<ai_sensitive_words> GetAll(string? category = null)
    {
        using var context = new StoryPlatformDbContext();
        IQueryable<ai_sensitive_words> q = context.ai_sensitive_words;
        if (!string.IsNullOrWhiteSpace(category))
            q = q.Where(w => w.category == category);
        return q.OrderBy(w => w.word).ToList();
    }

    public static ai_sensitive_words? GetById(Guid id)
    {
        using var context = new StoryPlatformDbContext();
        return context.ai_sensitive_words.FirstOrDefault(w => w.id == id);
    }

    public static void Add(ai_sensitive_words entity)
    {
        using var context = new StoryPlatformDbContext();
        if (entity.id == Guid.Empty) entity.id = Guid.NewGuid();
        if (entity.created_at == default) entity.created_at = DateTime.UtcNow;
        context.ai_sensitive_words.Add(entity);
        context.SaveChanges();
    }

    public static bool Delete(Guid id)
    {
        using var context = new StoryPlatformDbContext();
        var row = context.ai_sensitive_words.FirstOrDefault(w => w.id == id);
        if (row == null) return false;
        context.ai_sensitive_words.Remove(row);
        context.SaveChanges();
        return true;
    }
}
