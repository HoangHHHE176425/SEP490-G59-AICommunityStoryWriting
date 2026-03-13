using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

/// <summary>Bản nội dung do AI sinh ra (vd. từ đồng sáng tác). Chỉ lưu vào bảng này; khi tác giả tạo chương từ bản nháp thì gán chapter_id.</summary>
public partial class ai_generated_content
{
    public Guid id { get; set; }

    /// <summary>Truyện mà bản nháp thuộc về (bắt buộc khi tạo từ co-create; dùng để liệt kê bản nháp theo truyện).</summary>
    public Guid? story_id { get; set; }

    /// <summary>Gán khi tác giả tạo chương từ bản nháp này; null khi mới tạo từ co-create.</summary>
    public Guid? chapter_id { get; set; }

    public Guid? user_id { get; set; }

    /// <summary>Ý tưởng tác giả (input cho AI). Cột DB: input_prompt.</summary>
    public string? input_prompt { get; set; }

    /// <summary>Nội dung văn bản do AI sinh ra.</summary>
    public string? ai_output { get; set; }

    public DateTime? created_at { get; set; }

    public virtual chapters? chapter { get; set; }
}

