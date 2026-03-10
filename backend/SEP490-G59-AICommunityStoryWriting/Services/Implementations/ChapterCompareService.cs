using Microsoft.Extensions.Configuration;
using Repositories;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>So sánh chương tác giả với bản AI: embedding (cosine) hoặc text similarity.</summary>
public class ChapterCompareService : IChapterCompareService
{
    private const double SimilarityThresholdPercent = 85.0;

    private readonly IChapterRepository _chapterRepository;
    private readonly IAiGeneratedContentRepository _aiContentRepository;
    private readonly IStoryRepository _storyRepository;
    private readonly IConfiguration _configuration;

    public ChapterCompareService(
        IChapterRepository chapterRepository,
        IAiGeneratedContentRepository aiContentRepository,
        IStoryRepository storyRepository,
        IConfiguration configuration)
    {
        _chapterRepository = chapterRepository;
        _aiContentRepository = aiContentRepository;
        _storyRepository = storyRepository;
        _configuration = configuration;
    }

    public async Task<CompareChapterResponse> CompareAsync(CompareChapterRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        var chapter = _chapterRepository.GetById(request.ChapterId);
        if (chapter == null)
            return new CompareChapterResponse { HasBothContents = false, Message = "Không tìm thấy chương." };

        var story = chapter.story_id.HasValue ? _storyRepository.GetById(chapter.story_id.Value) : null;
        if (story != null && userId.HasValue && story.author_id != userId.Value)
            return new CompareChapterResponse { HasBothContents = false, Message = "Chỉ tác giả truyện được so sánh chương." };

        var authorContent = (chapter.content ?? "").Trim();
        var aiRecords = _aiContentRepository.GetAllByChapterId(request.ChapterId);

        if (string.IsNullOrEmpty(authorContent))
            return new CompareChapterResponse
            {
                HasBothContents = false,
                AuthorContentLength = 0,
                AiContentLength = 0,
                Message = "Nội dung chương trống."
            };
        if (aiRecords.Count == 0)
            return new CompareChapterResponse
            {
                HasBothContents = false,
                AuthorContentLength = authorContent.Length,
                AiContentLength = 0,
                Message = "Chưa có bản nội dung AI sinh ra cho chương này."
            };

        // So sánh với từng bản AI của chương, lấy điểm cao nhất (tác giả có thể đã chọn bản 1, bản 2 hay bản mới nhất).
        double bestScore = 0;
        int bestAiLength = 0;
        var config = EmbeddingHelper.GetEmbeddingConfig(_configuration);

        if (config.HasValue)
        {
            var (baseUrl, apiKey, model) = config.Value;
            var embAuthor = await EmbeddingHelper.GetEmbeddingAsync(authorContent, baseUrl, apiKey, model, cancellationToken);
            if (embAuthor.Length == 0)
            {
                foreach (var rec in aiRecords)
                {
                    var aiContent = (rec.ai_output ?? "").Trim();
                    if (string.IsNullOrEmpty(aiContent)) continue;
                    var s = TextSimilarityPercent(authorContent, aiContent);
                    if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
                }
            }
            else
            {
                foreach (var rec in aiRecords)
                {
                    var aiContent = (rec.ai_output ?? "").Trim();
                    if (string.IsNullOrEmpty(aiContent)) continue;
                    var embAi = await EmbeddingHelper.GetEmbeddingAsync(aiContent, baseUrl, apiKey, model, cancellationToken);
                    var s = embAi.Length > 0 ? CosineSimilarityPercent(embAuthor, embAi) : TextSimilarityPercent(authorContent, aiContent);
                    if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
                }
            }
        }
        else
        {
            foreach (var rec in aiRecords)
            {
                var aiContent = (rec.ai_output ?? "").Trim();
                if (string.IsNullOrEmpty(aiContent)) continue;
                var s = TextSimilarityPercent(authorContent, aiContent);
                if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
            }
        }

        var threshold = _configuration.GetValue("ChapterCompare:SimilarityThresholdPercent", SimilarityThresholdPercent);
        var roundedScore = Math.Round(bestScore, 2);

        // Lưu phần trăm giống nhau vào chapter khi chương đã PUBLISHED
        if (string.Equals(chapter.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
        {
            chapter.ai_similarity_percent = (decimal)roundedScore;
            _chapterRepository.Update(chapter);
        }

        return new CompareChapterResponse
        {
            SimilarityScore = roundedScore,
            IsSimilar = bestScore >= threshold,
            AuthorContentLength = authorContent.Length,
            AiContentLength = bestAiLength,
            HasBothContents = true,
            Message = bestScore >= threshold ? "Nội dung chương rất giống với bản AI." : "Nội dung chương khác so với bản AI."
        };
    }

    private static double CosineSimilarityPercent(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        var denom = Math.Sqrt(na) * Math.Sqrt(nb);
        if (denom <= 0) return 0;
        var cos = dot / denom;
        return Math.Clamp(cos, -1, 1) * 100.0;
    }

    private static double TextSimilarityPercent(string a, string b)
    {
        var setA = new HashSet<string>(SplitWords(a), StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(SplitWords(b), StringComparer.OrdinalIgnoreCase);
        if (setA.Count == 0 && setB.Count == 0) return 100;
        if (setA.Count == 0 || setB.Count == 0) return 0;
        int intersection = setA.Count(s => setB.Contains(s));
        int union = setA.Count + setB.Count - intersection;
        if (union == 0) return 100;
        return (intersection * 100.0) / union;
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (var w in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = w.Trim();
            if (t.Length > 0) yield return t;
        }
    }
}
