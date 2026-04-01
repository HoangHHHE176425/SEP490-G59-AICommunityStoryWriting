using BusinessObjects.Entities;

namespace Services.Helpers;

/// <summary>Cảnh báo khi tác giả gọi AI cho một slot chương trong khi vẫn có chương trước đó chưa PUBLISHED nhưng đã có nội dung — RAG chỉ dùng chương đã xuất bản.</summary>
public static class ChapterAiContextWarningHelper
{
    private const string WarningVi =
        "Hiện tại có chương trước đang ở trạng thái nháp hoặc chưa xuất bản (PUBLISHED) nhưng đã có nội dung; "
        + "bản AI sinh ra có thể không theo đúng mạch truyện của các chương đó. "
        + "Hãy chạy lại gợi ý hoặc đồng sáng tác sau khi các chương trước đã được xuất bản.";

    public static string? GetWarningIfApplicable(IEnumerable<chapters> storyChapters, int targetOrderIndex)
    {
        if (targetOrderIndex < 0)
            return null;
        foreach (var c in storyChapters)
        {
            if (c.order_index >= targetOrderIndex)
                continue;
            if (IsPublished(c))
                continue;
            if (string.IsNullOrWhiteSpace(c.content))
                continue;
            return WarningVi;
        }

        return null;
    }

    private static bool IsPublished(chapters c) =>
        string.Equals(c.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase);
}
