namespace Services.Helpers;

/// <summary>Chỉ thị ngôn ngữ cho prompt AI: nền tảng chỉ hỗ trợ nội dung truyện tiếng Việt (không tự phát hiện ngôn ngữ từ context).</summary>
public static class StoryLanguageHelper
{
    /// <summary>Đưa vào user prompt cùng các agent; bắt buộc mọi output liên quan truyện bằng tiếng Việt.</summary>
    public const string VietnameseOnlyInstruction =
        "Ngôn ngữ: Tiếng Việt. Viết toàn bộ nội dung sinh ra (dàn ý, chương, gợi ý, feedback) bằng tiếng Việt. Không được xen bất kỳ từ hoặc cụm từ thuộc ngôn ngữ khác; mọi từ phải thuần tiếng Việt.";
}
