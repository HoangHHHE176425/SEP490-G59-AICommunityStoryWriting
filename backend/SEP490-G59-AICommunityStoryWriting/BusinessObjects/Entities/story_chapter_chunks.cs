using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

/// <summary>Chunk của chương truyện dùng cho RAG: vectorize → lưu embedding → retrieve theo query (ý tưởng tác giả).</summary>
public partial class story_chapter_chunks
{
    public Guid id { get; set; }

    public Guid story_id { get; set; }

    public Guid chapter_id { get; set; }

    /// <summary>Thứ tự đoạn trong chương (0-based).</summary>
    public int chunk_index { get; set; }

    /// <summary>Nội dung đoạn (văn bản đã cắt).</summary>
    public string content { get; set; } = null!;

    /// <summary>Embedding vector lưu dạng JSON array of float (ví dụ [0.1, -0.2, ...]). Dùng cho similarity search.</summary>
    public string? embedding_json { get; set; }

    /// <summary>Model embedding đã dùng (ví dụ text-embedding-3-small) để biết dimension khi tái index.</summary>
    public string? embedding_model { get; set; }

    public DateTime? created_at { get; set; }

    public virtual chapters chapter { get; set; } = null!;

    public virtual stories story { get; set; } = null!;
}
