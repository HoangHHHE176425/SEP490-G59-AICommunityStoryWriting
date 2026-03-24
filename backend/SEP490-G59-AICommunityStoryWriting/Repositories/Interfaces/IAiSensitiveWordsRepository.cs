using BusinessObjects.Entities;

namespace Repositories;

/// <summary>Từ cấm / từ nhạy cảm (ai_sensitive_words). Dùng cho check-chapter; admin quản lý qua API.</summary>
public interface IAiSensitiveWordsRepository
{
    /// <summary>Lấy tất cả từ. category = "BannedWord" cho từ cấm check-chapter.</summary>
    IReadOnlyList<ai_sensitive_words> GetAll(string? category = null);

    ai_sensitive_words? GetById(Guid id);

    void Add(ai_sensitive_words entity);

    bool Delete(Guid id);
}
