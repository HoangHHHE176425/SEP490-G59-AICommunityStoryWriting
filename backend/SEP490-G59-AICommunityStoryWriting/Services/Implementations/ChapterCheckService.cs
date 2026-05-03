using System.Text.Json;
using System.Security.Cryptography;
using System.Text;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using OpenAI.Chat;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations;

/// <summary>Kiểm tra chương: chính tả (AI) và từ cấm (BannedWords).</summary>
public class ChapterCheckService : IChapterCheckService
{
    /// <summary>Kết quả kiểm tra chính tả đã parse (cache theo toàn bộ nội dung).</summary>
    private sealed class SpellCheckMerged
    {
        public List<SpellingIssue> Issues { get; init; } = new();
        public string? Summary { get; init; }
    }

    private const string ActionChapterCheck = "CHAPTER_CHECK";
    private const string ActionChapterCheckBanned = "CHAPTER_CHECK_BANNED";
    private static readonly TimeSpan SpellCacheTtl = TimeSpan.FromMinutes(10);
    // Common Vietnamese orthographic variants accepted in modern usage; do not flag as typo.
    private static readonly HashSet<string> AcceptedVariantPairs = new(StringComparer.OrdinalIgnoreCase)
    {
        "kì|kỳ", "kỳ|kì",
        "lí|lý", "lý|lí",
        "mĩ|mỹ", "mỹ|mĩ",
        "quí|quý", "quý|quí"
    };

    private readonly IAIUsageLogRepository _aiUsageLogRepository;
    private readonly IConfiguration _configuration;
    private readonly IContentGuardrailService _guardrail;
    private readonly IMemoryCache _cache;

    public ChapterCheckService(
        IAIUsageLogRepository aiUsageLogRepository,
        IConfiguration configuration,
        IContentGuardrailService guardrail,
        IMemoryCache cache)
    {
        _aiUsageLogRepository = aiUsageLogRepository;
        _configuration = configuration;
        _guardrail = guardrail;
        _cache = cache;
    }

    /// <summary>Gộp từ cấm + chính tả (chỉ dùng cho unit test ma trận; API công khai đã tách hai endpoint).</summary>
    internal async Task<CheckChapterResponse> CheckAsync(CheckChapterSpellingRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return new CheckChapterResponse { Passed = true, Summary = "Nội dung trống, không cần kiểm tra." };

        var content = NormalizeContentForCheck(request.Content);
        var storyId = request.StoryId ?? Guid.Empty;
        var policyViolations = await CollectPolicyViolationsAsync(storyId, content, cancellationToken);

        var (spellingIssues, summary, spellRawError) = await RunSpellingCheckInternalAsync(request.ChapterTitle, content, cancellationToken);

        var (_, model, _, _) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);
        if (userId.HasValue)
        {
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = userId.Value,
                story_id = request.StoryId,
                chapter_id = null,
                action_type = ActionChapterCheck,
                model_name = model,
                prompt_tokens = 0,
                completion_tokens = 0,
                total_tokens = 0,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });
        }

        if (spellRawError != null)
            return new CheckChapterResponse
            {
                Passed = policyViolations.Count == 0,
                PolicyViolations = policyViolations,
                Summary = spellRawError
            };

        var passed = spellingIssues.Count == 0 && policyViolations.Count == 0;
        return new CheckChapterResponse
        {
            Passed = passed,
            SpellingIssues = spellingIssues,
            PolicyViolations = policyViolations,
            HasInappropriateContent = false,
            Summary = summary
        };
    }
    //check tu cam
    public async Task<CheckChapterResponse> CheckBannedWordsOnlyAsync(CheckChapterBannedWordsRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return new CheckChapterResponse { Passed = true, Summary = "Nội dung trống, không cần kiểm tra." };

        var content = NormalizeContentForCheck(request.Content);
        var storyId = request.StoryId ?? Guid.Empty;
        var policyViolations = await CollectPolicyViolationsAsync(storyId, content, cancellationToken);

        if (userId.HasValue)
        {
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = userId.Value,
                story_id = request.StoryId,
                chapter_id = null,
                action_type = ActionChapterCheckBanned,
                model_name = "BannedWords",
                prompt_tokens = 0,
                completion_tokens = 0,
                total_tokens = 0,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });
        }

        var passed = policyViolations.Count == 0;
        return new CheckChapterResponse
        {
            Passed = passed,
            SpellingIssues = new List<SpellingIssue>(),
            PolicyViolations = policyViolations,
            HasInappropriateContent = false,
            Summary = passed
                ? "Không phát hiện từ cấm hoặc vi phạm theo danh sách."
                : "Nội dung có từ cấm/vi phạm chính sách. Xem policyViolations."
        };
    }
    //check chinh ta
    public async Task<CheckChapterResponse> CheckSpellingOnlyAsync(CheckChapterSpellingRequest request, Guid? userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return new CheckChapterResponse { Passed = true, Summary = "Nội dung trống, không cần kiểm tra." };

        var content = NormalizeContentForCheck(request.Content);

        var (spellingIssues, summary, spellRawError) = await RunSpellingCheckInternalAsync(request.ChapterTitle, content, cancellationToken);

        if (userId.HasValue)
        {
            var (_, model, _, _) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = userId.Value,
                story_id = request.StoryId,
                chapter_id = null,
                action_type = ActionChapterCheck,
                model_name = model,
                prompt_tokens = 0,
                completion_tokens = 0,
                total_tokens = 0,
                status = "SUCCESS_SPELL_ONLY",
                created_at = DateTime.UtcNow
            });
        }

        if (spellRawError != null)
            return new CheckChapterResponse
            {
                Passed = true,
                SpellingIssues = new List<SpellingIssue>(),
                Summary = spellRawError
            };

        return new CheckChapterResponse
        {
            Passed = spellingIssues.Count == 0,
            SpellingIssues = spellingIssues,
            PolicyViolations = new List<PolicyViolationItem>(),
            Summary = summary
        };
    }

    private static string NormalizeContentForCheck(string raw)
    {
        return ChapterContentNormalizer.NormalizeForAi(raw, 50000);
    }

    private async Task<List<PolicyViolationItem>> CollectPolicyViolationsAsync(
        Guid storyId,
        string content,
        CancellationToken cancellationToken)
    {
        var guardrailResult = await _guardrail.CheckAsync(storyId, content, cancellationToken);
        var policyViolations = new List<PolicyViolationItem>();
        foreach (var v in guardrailResult.Violations)
            policyViolations.Add(new PolicyViolationItem { Type = v.Type, Description = v.Message, Quote = v.Quote });
        return policyViolations;
    }

    /// <summary>Gọi AI chính tả theo từng đoạn (nội dung dài) + cache kết quả đã gộp.</summary>
    private async Task<(List<SpellingIssue> Issues, string? Summary, string? RawError)> RunSpellingCheckInternalAsync(
        string? chapterTitle,
        string content,
        CancellationToken cancellationToken)
    {
        var cacheKey = BuildSpellCacheKey(chapterTitle, content);
        if (_cache.TryGetValue(cacheKey, out SpellCheckMerged? merged) && merged != null)
            return (merged.Issues, merged.Summary, null);

        var maxChunk = _configuration.GetValue("ChapterCheck:SpellChunkMaxChars", 3200);
        if (maxChunk < 800) maxChunk = 3200;

        var overlapChars = _configuration.GetValue("ChapterCheck:SpellChunkOverlapChars", 200);
        overlapChars = Math.Max(0, overlapChars);

        var parallelism = _configuration.GetValue("ChapterCheck:SpellChunkParallelism", 4);
        parallelism = Math.Clamp(parallelism, 1, 16);

        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentConsistencyChecker);
        options ??= new ChatCompletionOptions();
        options.Temperature = 0;
        options.TopP = 1;

        var chunks = SplitIntoSpellChunks(content, maxChunk, overlapChars);
        var allIssues = new List<SpellingIssue>();
        var chunkSummaries = new List<string>();

        if (chunks.Count == 1 || parallelism == 1)
        {
            for (var ci = 0; ci < chunks.Count; ci++)
            {
                var (chunkIssues, chunkSummary, rawErr) = await RunSpellChunkOnceAsync(
                    client, options, chapterTitle, chunks[ci], ci + 1, chunks.Count, content, cancellationToken);
                if (rawErr != null)
                    return (new List<SpellingIssue>(), null, rawErr);
                allIssues.AddRange(chunkIssues);
                if (!string.IsNullOrWhiteSpace(chunkSummary))
                    chunkSummaries.Add(chunkSummary.Trim());
            }
        }
        else
        {
            using var gate = new SemaphoreSlim(parallelism, parallelism);
            var indexed = chunks.Select((c, i) => (Chunk: c, Index: i)).ToArray();
            var tasks = indexed.Select(async item =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    return await RunSpellChunkOnceAsync(
                        client,
                        options,
                        chapterTitle,
                        item.Chunk,
                        item.Index + 1,
                        chunks.Count,
                        content,
                        cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    gate.Release();
                }
            });
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            foreach (var (chunkIssues, chunkSummary, rawErr) in results)
            {
                if (rawErr != null)
                    return (new List<SpellingIssue>(), null, rawErr);
                allIssues.AddRange(chunkIssues);
                if (!string.IsNullOrWhiteSpace(chunkSummary))
                    chunkSummaries.Add(chunkSummary.Trim());
            }
        }

        var deduped = MergeExpandAndDedupeSpellIssues(allIssues, content);
        var finalSummary = BuildMergedSpellSummary(deduped.Count, chunks.Count, chunkSummaries);

        merged = new SpellCheckMerged { Issues = deduped, Summary = finalSummary };
        _cache.Set(cacheKey, merged, SpellCacheTtl);
        return (deduped, finalSummary, null);
    }

    private static async Task<(List<SpellingIssue> Issues, string? Summary, string? RawError)> RunSpellChunkOnceAsync(
        ChatClient client,
        ChatCompletionOptions options,
        string? chapterTitle,
        string chunk,
        int chunkIndex,
        int chunkCount,
        string fullChapterContent,
        CancellationToken cancellationToken)
    {
        var userPrompt = BuildSpellCheckUserPrompt(chapterTitle, chunk, chunkIndex, chunkCount);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(GetSystemPrompt()),
            new UserChatMessage(userPrompt)
        };

        var completion = await client.CompleteChatAsync(messages, options, cancellationToken).ConfigureAwait(false);
        var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;

        if (string.IsNullOrWhiteSpace(text))
            return (new List<SpellingIssue>(), null, "Không đọc được kết quả kiểm tra chính tả từ AI.");

        var (chunkIssues, chunkSummary) = ParseSpellingResponse(text, fullChapterContent);
        return (chunkIssues, chunkSummary, null);
    }

    private static string BuildSpellCheckUserPrompt(string? chapterTitle, string chunkBody, int chunkIndex, int chunkCount)
    {
        var titlePart = string.IsNullOrWhiteSpace(chapterTitle) ? "" : $"Tiêu đề chương: {chapterTitle}\n\n";
        var scopeNote = chunkCount > 1
            ? $"(Đoạn {chunkIndex}/{chunkCount} — chỉ báo lỗi chính tả trong đoạn dưới đây, không suy diễn ngoài đoạn.)\n\n"
            : "";

        return $@"{titlePart}{scopeNote}Nội dung cần kiểm tra (chính tả / đánh máy):

---
{chunkBody}
---

Nhiệm vụ: tìm mọi lỗi chính tả hoặc typo trong đoạn trên (tiếng Việt và từ/cụm tiếng Anh nếu có trong văn bản).

Tiêu chí báo cáo (ưu tiên đủ lỗi có căn cứ, không tự giới hạn số lượng; mỗi lỗi một phần tử trong spellingErrors):
- Sai dấu thanh, dấu mũ, hoặc nhầm vần rõ rệt (ví dụ: một dấu sai khiến từ không còn là từ đúng chuẩn).
- Nhầm phụ âm/âm trong tiếng Việt thuần: l/n, ch/tr, s/x, d/gi/r; i/y khi rõ là lỗi gõ trong từ Việt (không đụng từ mượn/tiếng Anh hợp lệ).
- Chữ Latin bất thường trong từ/cụm tiếng Việt (ví dụ j, w, f thay cho đ/g/ph/qu khi rõ là nhầm bàn phím hoặc không thuộc bảng chữ thường dùng cho tiếng Việt).
- Lỗi gõ: ký tự thừa/thiếu, nhầm phím liền kề, sai hoa/thường giữa câu khi rõ là lỗi (không phải chủ đích văn chương).

Bỏ qua (không đưa vào spellingErrors):
- Tên riêng, biệt danh, địa danh, thuật ngữ hư cấu do tác giả đặt (trừ khi lỗi là sai chính tả chuẩn tiếng Việt trong cụm không phải tên).
- Tiếng Anh/từ nước ngoài đúng chính tả nguồn gốc.
- Hai cách viết đều hợp lệ; hoặc không phân biệt được typo và chủ đích nghệ thuật.

Ràng buộc nghiệp vụ:
- Cùng một wordOrPhrase và suggestion xuất hiện ở nhiều câu khác nhau: một phần tử JSON cho cặp đó là đủ; backend sẽ tách thành nhiều dòng kết quả, mỗi dòng kèm đúng câu chứa lỗi tương ứng.
- Không paraphrase, không biên tập câu, không đổi văn phong, ngữ nghĩa, xưng hô hay đại từ.
- Không thay từ đúng bằng từ đồng nghĩa; không bịa lỗi.
- suggestion: sửa tối thiểu (chỉ phần sai), giữ nguyên nghĩa câu.
- wordOrPhrase: đúng nguyên văn từ/cụm sai trong đoạn.
- context: copy NGUYÊN VĂN **cả câu** (hoặc cả dòng thoại) chứa wordOrPhrase — từ đầu câu tới hết câu (dấu . ! ? … hoặc xuống dòng đoạn); BẮT BUỘC chứa chính xác wordOrPhrase (kể cả ký tự sai).
- Không đủ wordOrPhrase + context hợp lệ thì không trả lỗi đó.

Đầu ra: DUY NHẤT một JSON hợp lệ, không markdown, không ```, không text ngoài JSON:
{{ ""spellingErrors"": [ {{ ""wordOrPhrase"": ""..."", ""suggestion"": ""..."", ""context"": ""..."" }} ], ""summary"": ""Tóm tắt ngắn (1–2 câu), ghi số lỗi hoặc xác nhận không có lỗi"" }}

Nếu không có lỗi đủ điều kiện: spellingErrors = [] và summary = ""Không phát hiện lỗi chính tả.""";
    }

    /// <summary>Gộp tóm tắt từng đoạn; ưu tiên số lỗi thực tế đã parse.</summary>
    private static string BuildMergedSpellSummary(int issueCount, int chunkCount, IReadOnlyList<string> chunkSummaries)
    {
        if (issueCount > 0)
        {
            return chunkCount > 1
                ? $"Phát hiện {issueCount} lỗi chính tả (đã kiểm tra theo {chunkCount} đoạn để giảm bỏ sót)."
                : $"Phát hiện {issueCount} lỗi chính tả.";
        }
        return "Không phát hiện lỗi chính tả.";
    }

    /// <summary>Chia nội dung dài thành khối ≤ maxChars, ưu tiên cắt tại xuống dòng; overlap giảm sót lỗi ở ranh giới chunk.</summary>
    private static IReadOnlyList<string> SplitIntoSpellChunks(string content, int maxChars, int overlapChars)
    {
        if (maxChars < 800) maxChars = 800;
        overlapChars = Math.Max(0, Math.Min(overlapChars, Math.Max(0, maxChars / 2 - 1)));

        content = content.Replace("\r\n", "\n", StringComparison.Ordinal);
        if (content.Length <= maxChars)
            return new[] { content };

        var list = new List<string>();
        var start = 0;
        while (start < content.Length)
        {
            var remaining = content.Length - start;
            if (remaining <= maxChars)
            {
                var tail = content[start..].TrimEnd();
                if (tail.Length > 0) list.Add(tail);
                break;
            }

            var splitAt = start + maxChars;
            var low = start + maxChars / 2;
            var foundNl = false;
            for (var k = splitAt - 1; k >= low; k--)
            {
                if (content[k] != '\n') continue;
                splitAt = k + 1;
                foundNl = true;
                break;
            }

            if (!foundNl)
                splitAt = start + maxChars;

            var piece = content[start..splitAt].TrimEnd();
            if (piece.Length > 0)
                list.Add(piece);

            if (splitAt >= content.Length)
                break;

            var nextStart = overlapChars > 0 ? Math.Max(splitAt - overlapChars, start + 1) : splitAt;
            start = nextStart;
            while (start < content.Length && content[start] == '\n') start++;
        }

        return list.Count > 0 ? list : new[] { content };
    }

    private static string GetSystemPrompt()
    {
        return """
Bạn là trình kiểm tra chính tả và lỗi đánh máy (typo) cho văn bản chương truyện.

Nguyên tắc: chỉ báo lỗi khi có bằng chứng trực tiếp trên nguyên văn đoạn được gửi (từ/cụm sai thật sự xuất hiện trong đó). Ưu tiên liệt kê đủ các lỗi typo/chính tả rõ ràng; không tự giới hạn chỉ một vài lỗi.

TUYỆT ĐỐI CẤM: đổi văn phong, ngữ nghĩa, xưng hô, đại từ, biên tập lại câu, diễn giải, paraphrase, hoặc thay từ đúng bằng từ đồng nghĩa “hay hơn”.

Mỗi lỗi phải có wordOrPhrase khớp nguyên văn và context là **cả câu** (hoặc cả dòng thoại) copy nguyên văn từ đoạn user gửi, bắt buộc chứa đúng wordOrPhrase (kể cả ký tự sai). Thiếu một trong hai thì không được trả lỗi đó.

Đầu ra: DUY NHẤT một JSON hợp lệ, không markdown, không giải thích ngoài JSON, đúng schema:
{ "spellingErrors": [ { "wordOrPhrase": "...", "suggestion": "...", "context": "..." } ], "summary": "..." }
""";
    }

    private static (List<SpellingIssue> SpellingIssues, string? Summary) ParseSpellingResponse(string text, string chapterContent)
    {
        text = text.Trim();
        if (text.StartsWith("```"))
        {
            var start = text.IndexOf('\n') + 1;
            var end = text.IndexOf("```", start, StringComparison.Ordinal);
            if (end > start)
                text = text[start..end];
        }

        try
        {
            var root = JsonDocument.Parse(text).RootElement;
            var spelling = new List<SpellingIssue>();
            if (root.TryGetProperty("spellingErrors", out var se) && se.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in se.EnumerateArray())
                {
                    var rawWordOrPhrase = item.TryGetProperty("wordOrPhrase", out var w) ? w.GetString() ?? "" : "";
                    var wordOrPhrase = rawWordOrPhrase;
                    var suggestion = item.TryGetProperty("suggestion", out var s) ? s.GetString() ?? "" : "";
                    var context = item.TryGetProperty("context", out var c) ? c.GetString() : null;
                    var punctuationLike = IsLikelyPunctuationIssue(wordOrPhrase, suggestion, context);

                    var chapterAnchored = IsChapterAnchoredTypo(wordOrPhrase, suggestion, context, chapterContent, punctuationLike);
                    if (!IsLikelyTypoCorrection(wordOrPhrase, suggestion) && !punctuationLike && !chapterAnchored)
                        continue;
                    if (IsAcceptedVariantPair(wordOrPhrase, suggestion))
                        continue;

                    var needleForExtract = rawWordOrPhrase.Trim();
                    if (!punctuationLike &&
                        !string.IsNullOrEmpty(needleForExtract) &&
                        !IsPlaceholderTypoLabel(needleForExtract))
                    {
                        var fromChapter = SentenceContextExtractor.TryExtractSentenceContainingNeedle(chapterContent, needleForExtract);
                        if (!string.IsNullOrEmpty(fromChapter))
                            context = fromChapter;
                        else
                        {
                            var ctxTrim = (context ?? "").Trim();
                            if (string.IsNullOrEmpty(ctxTrim) ||
                                !ctxTrim.Contains(needleForExtract, StringComparison.OrdinalIgnoreCase))
                            {
                                var extracted = SentenceContextExtractor.TryShortContextSnippetContainingNeedle(chapterContent, needleForExtract);
                                if (!string.IsNullOrEmpty(extracted))
                                    context = extracted;
                            }
                            else
                            {
                                var fromCtx = SentenceContextExtractor.TryExtractSentenceContainingNeedle(ctxTrim, needleForExtract);
                                context = !string.IsNullOrEmpty(fromCtx) ? fromCtx : ctxTrim;
                            }
                        }
                    }

                    if (!punctuationLike &&
                        !string.IsNullOrWhiteSpace(context) &&
                        !string.IsNullOrWhiteSpace(wordOrPhrase) &&
                        !context.Contains(wordOrPhrase, StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (string.IsNullOrWhiteSpace(wordOrPhrase))
                        wordOrPhrase = punctuationLike ? "Lỗi dấu câu" : "Lỗi chính tả";
                    if (string.IsNullOrWhiteSpace(suggestion))
                        suggestion = punctuationLike ? "Rà soát và chỉnh lại dấu câu theo câu/dòng được trích bên dưới." : "Rà soát và chỉnh lại từ/cụm này.";

                    spelling.Add(new SpellingIssue
                    {
                        WordOrPhrase = wordOrPhrase,
                        Suggestion = suggestion,
                        Context = context
                    });
                }
            }
            var summary = root.TryGetProperty("summary", out var sum) ? sum.GetString() : null;
            var dedup = DeduplicateIssues(spelling);
            if (dedup.Count == 0 && SummaryIndicatesSpellingIssue(summary))
                summary = "Không trích xuất được từ sai cụ thể từ phản hồi AI. Vui lòng chạy lại kiểm tra.";
            return (dedup, summary);
        }
        catch
        {
            return (new List<SpellingIssue>(), "Định dạng phản hồi không hợp lệ.");
        }
    }

    /// <summary>Label do hệ thống đặt khi thiếu từ cụ thể — không dùng để trích câu.</summary>
    private static bool IsPlaceholderTypoLabel(string wordOrPhrase)
    {
        var w = (wordOrPhrase ?? "").Trim();
        return w.Equals("Lỗi chính tả", StringComparison.OrdinalIgnoreCase)
               || w.Equals("Lỗi dấu câu", StringComparison.OrdinalIgnoreCase)
               || w.Equals("Lỗi chính tả/dấu câu", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Bỏ qua bộ lọc Levenshtein khi AI đã neo lỗi bằng trích đoạn copy nguyên văn khớp cả chương (giảm sót gợi ý hợp lệ tiếng Việt).
    /// </summary>
    private static bool IsChapterAnchoredTypo(
        string wordOrPhrase,
        string suggestion,
        string? context,
        string chapterContent,
        bool punctuationLike)
    {
        if (punctuationLike) return false;
        if (string.IsNullOrWhiteSpace(wordOrPhrase) || string.IsNullOrWhiteSpace(suggestion)) return false;
        var needle = wordOrPhrase.Trim();
        if (needle.Length == 0 || IsPlaceholderTypoLabel(needle)) return false;
        if (string.Equals(needle, suggestion.Trim(), StringComparison.OrdinalIgnoreCase)) return false;

        if (!chapterContent.Contains(needle, StringComparison.OrdinalIgnoreCase)) return false;

        var ctx = (context ?? "").Trim();
        if (ctx.Length < 12 || !ctx.Contains(needle, StringComparison.OrdinalIgnoreCase)) return false;

        return chapterContent.Contains(ctx, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSpellCacheKey(string? title, string content)
    {
        var normalized = NormalizeForCache($"{title ?? ""}\n{content}");
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return $"chapter-check:spell:{Convert.ToHexString(bytes)}";
    }

    private static string NormalizeForCache(string text)
    {
        var chars = text.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ')
            .ToArray();
        return string.Join(' ', new string(chars).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool IsAcceptedVariantPair(string wordOrPhrase, string suggestion)
    {
        var left = (wordOrPhrase ?? "").Trim().ToLowerInvariant();
        var right = (suggestion ?? "").Trim().ToLowerInvariant();
        if (left.Length == 0 || right.Length == 0) return false;
        return AcceptedVariantPairs.Contains($"{left}|{right}");
    }

    /// <summary>Gộp kết quả nhiều chunk: mỗi cặp (từ sai, gợi ý) chỉ mở rộng một lần trên toàn chương → một dòng cho mỗi lần xuất hiện, mỗi dòng một câu ngữ cảnh.</summary>
    private static List<SpellingIssue> MergeExpandAndDedupeSpellIssues(List<SpellingIssue> allIssues, string chapterContent)
    {
        if (allIssues.Count == 0) return allIssues;

        var punctuationLike = new List<SpellingIssue>();
        var typoLike = new List<SpellingIssue>();
        foreach (var issue in allIssues)
        {
            if (ShouldSkipOccurrenceExpansion(issue))
                punctuationLike.Add(issue);
            else
                typoLike.Add(issue);
        }

        var expanded = new List<SpellingIssue>();
        var seenTypoPair = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var issue in typoLike)
        {
            var w = issue.WordOrPhrase?.Trim() ?? "";
            var s = issue.Suggestion?.Trim() ?? "";
            if (w.Length == 0)
            {
                expanded.Add(issue);
                continue;
            }

            var pairKey = $"{w}|{s}";
            if (!seenTypoPair.Add(pairKey))
                continue;

            expanded.AddRange(ExpandTypoToAllOccurrencesInChapter(issue, chapterContent));
        }

        expanded.AddRange(punctuationLike);
        return DeduplicateIssues(expanded);
    }

    private static bool ShouldSkipOccurrenceExpansion(SpellingIssue issue)
    {
        var w = issue.WordOrPhrase?.Trim() ?? "";
        if (IsPlaceholderTypoLabel(w))
            return true;
        return IsLikelyPunctuationIssue(w, issue.Suggestion ?? "", issue.Context);
    }

    private static List<SpellingIssue> ExpandTypoToAllOccurrencesInChapter(SpellingIssue template, string chapterContent)
    {
        var w = template.WordOrPhrase?.Trim() ?? "";
        if (w.Length == 0 || !chapterContent.Contains(w, StringComparison.OrdinalIgnoreCase))
            return new List<SpellingIssue> { template };

        var list = new List<SpellingIssue>();
        var pos = 0;
        while (true)
        {
            var idx = chapterContent.IndexOf(w, pos, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) break;

            var sent = SentenceContextExtractor.TryExtractSentenceContainingNeedleAt(chapterContent, w, idx);
            if (string.IsNullOrEmpty(sent) || !sent.Contains(w, StringComparison.OrdinalIgnoreCase))
            {
                var fb = SentenceContextExtractor.TryExtractContextSnippetAt(chapterContent, w, idx);
                sent = !string.IsNullOrEmpty(fb)
                    ? fb
                    : template.Context?.Trim() ?? "";
            }

            list.Add(new SpellingIssue
            {
                WordOrPhrase = template.WordOrPhrase ?? "",
                Suggestion = template.Suggestion ?? "",
                Context = sent
            });

            pos = idx + Math.Max(1, w.Length);
        }

        return list.Count > 0 ? list : new List<SpellingIssue> { template };
    }

    private static List<SpellingIssue> DeduplicateIssues(List<SpellingIssue> issues)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SpellingIssue>();
        foreach (var i in issues)
        {
            var key = $"{i.WordOrPhrase?.Trim()}|{i.Suggestion?.Trim()}|{i.Context?.Trim()}";
            if (set.Add(key))
                result.Add(i);
        }
        return result;
    }

    private static bool IsLikelyTypoCorrection(string wordOrPhrase, string suggestion)
    {
        wordOrPhrase = (wordOrPhrase ?? "").Trim();
        suggestion = (suggestion ?? "").Trim();
        if (wordOrPhrase.Length == 0 || suggestion.Length == 0) return false;
        if (string.Equals(wordOrPhrase, suggestion, StringComparison.OrdinalIgnoreCase)) return false;

        var wc1 = CountWords(wordOrPhrase);
        var wc2 = CountWords(suggestion);
        if (wc1 == 0 || wc2 == 0) return false;
        if (wc1 != wc2) return false;

        var len1 = wordOrPhrase.Length;
        var len2 = suggestion.Length;
        var maxLen = Math.Max(len1, len2);
        // Từ dài (tiếng Việt): cho phép lệch ký tự / khoảng cách chỉnh sửa lớn hơn một chút.
        var maxLenDiff = maxLen >= 8 ? 5 : 3;
        var maxDist = maxLen >= 8 ? 4 : 3;
        if (Math.Abs(len1 - len2) > maxLenDiff) return false;

        var dist = LevenshteinDistance(
            wordOrPhrase.ToLowerInvariant(),
            suggestion.ToLowerInvariant(),
            maxDistance: maxDist);
        return dist >= 1 && dist <= maxDist;
    }

    private static bool IsLikelyPunctuationIssue(string wordOrPhrase, string suggestion, string? context)
    {
        var w = (wordOrPhrase ?? "").Trim();
        var s = (suggestion ?? "").Trim();
        var c = (context ?? "").Trim();
        var full = $"{w} {s} {c}".ToLowerInvariant();
        if (full.Length == 0) return false;

        // Dấu câu xuất hiện trực tiếp.
        if (ContainsPunctuationToken(w) || ContainsPunctuationToken(s))
            return true;

        // Hoặc mô tả bằng từ khóa.
        return full.Contains("dấu câu")
            || full.Contains("dấu phẩy")
            || full.Contains("dấu chấm")
            || full.Contains("dấu hai chấm")
            || full.Contains("dấu chấm phẩy")
            || full.Contains("dấu chấm hỏi")
            || full.Contains("dấu chấm than")
            || full.Contains("dấu ngoặc");
    }

    private static bool ContainsPunctuationToken(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;
        foreach (var ch in text)
        {
            if (char.IsPunctuation(ch)) return true;
        }
        return false;
    }

    private static bool SummaryIndicatesSpellingIssue(string? summary)
    {
        var s = (summary ?? "").Trim().ToLowerInvariant();
        if (s.Length == 0) return false;
        if (s.Contains("không phát hiện")) return false;
        if (s.Contains("không có lỗi chính tả") || s.Contains("không còn lỗi chính tả")) return false;
        return s.Contains("lỗi chính tả") || s.Contains("dấu câu");
    }

    private static int CountWords(string s)
        => s.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;

    private static int LevenshteinDistance(string a, string b, int maxDistance)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        if (Math.Abs(a.Length - b.Length) > maxDistance) return maxDistance + 1;

        var prev = new int[b.Length + 1];
        var curr = new int[b.Length + 1];
        for (var j = 0; j <= b.Length; j++) prev[j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            curr[0] = i;
            var minInRow = curr[0];
            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                curr[j] = Math.Min(
                    Math.Min(curr[j - 1] + 1, prev[j] + 1),
                    prev[j - 1] + cost);
                if (curr[j] < minInRow) minInRow = curr[j];
            }

            if (minInRow > maxDistance) return maxDistance + 1;
            (prev, curr) = (curr, prev);
        }

        return prev[b.Length];
    }
}
