namespace Services.DTOs.AI;

/// <summary>Request index RAG cho một truyện (chunk + embedding).</summary>
public class IndexRagRequest
{
    public Guid StoryId { get; set; }
    /// <summary>Chỉ index các chương có order_index &lt;= chương này. Null = index tất cả.</summary>
    public Guid? AfterChapterId { get; set; }
}
