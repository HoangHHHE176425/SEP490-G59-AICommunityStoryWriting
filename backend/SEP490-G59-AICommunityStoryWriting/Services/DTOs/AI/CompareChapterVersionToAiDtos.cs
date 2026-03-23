namespace Services.DTOs.AI;

/// <summary>So sánh <c>content_snapshot</c> (phiên bản mới nhất) với <c>ai_generated_content.ai_output</c> theo <c>chapter_id</c>.</summary>
public class CompareChapterVersionToAiRequest
{
    public Guid ChapterId { get; set; }
}

public class CompareChapterVersionToAiResponse
{
    public double SimilarityScore { get; set; }
    public bool IsSimilar { get; set; }
    public int SnapshotContentLength { get; set; }
    public int AiContentLength { get; set; }
    public bool HasBothContents { get; set; }
    public string? Message { get; set; }
    /// <summary>Phiên bản đã cập nhật trường <c>ai_similarity_percent</c>.</summary>
    public Guid? VersionId { get; set; }
    public int? VersionNumber { get; set; }
}
