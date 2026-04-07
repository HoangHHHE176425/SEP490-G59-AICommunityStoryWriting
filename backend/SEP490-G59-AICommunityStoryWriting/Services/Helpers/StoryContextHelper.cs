using BusinessObjects.Entities;

namespace Services.Helpers;

/// <summary>Xây dựng ngữ cảnh từ N chương gần nhất (theo order_index) để dùng cho đồng sáng tác và kiểm tra nhất quán, không cần RAG/embedding.</summary>
public static class StoryContextHelper
{
    /// <summary>Lấy tối đa lastN chương (có order_index lớn nhất), mỗi chương giới hạn maxCharsPerChapter ký tự. Trả về block văn bản để đưa vào prompt; trả về rỗng nếu không có chương.</summary>
    public static string BuildLastChaptersContext(IEnumerable<chapters> chaptersOrderedByIndex, int lastN = 5, int maxCharsPerChapter = 2600)
    {
        var list = chaptersOrderedByIndex as IList<chapters> ?? chaptersOrderedByIndex.ToList();
        if (list.Count == 0)
            return string.Empty;

        var takeCount = Math.Min(lastN, list.Count);
        var lastChapters = list.Count <= takeCount ? list : list.Skip(list.Count - takeCount).ToList();
        var lines = new List<string>();

        foreach (var ch in lastChapters)
        {
            var title = ch.title ?? $"Chương {ch.order_index}";
            var content = ChapterContentNormalizer.NormalizeForAi(ch.content, maxCharsPerChapter);
            lines.Add($"[Chương {ch.order_index}: {title}]\n{content.Trim()}");
        }

        return string.Join("\n\n", lines);
    }
}
