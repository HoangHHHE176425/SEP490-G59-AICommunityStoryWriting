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

        var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfigForAgent(_configuration, AIClientHelper.AgentConsistencyChecker);
        var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);
        var options = AIClientHelper.GetCompletionOptions(_configuration, AIClientHelper.AgentConsistencyChecker);
        options ??= new ChatCompletionOptions();
        options.Temperature = 0;
        options.TopP = 1;

        var chunks = SplitIntoSpellChunks(content, maxChunk);
        var allIssues = new List<SpellingIssue>();
        var chunkSummaries = new List<string>();

        for (var ci = 0; ci < chunks.Count; ci++)
        {
            var chunk = chunks[ci];
            var userPrompt = BuildSpellCheckUserPrompt(chapterTitle, chunk, ci + 1, chunks.Count);
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetSystemPrompt()),
                new UserChatMessage(userPrompt)
            };

            var completion = await client.CompleteChatAsync(messages, options, cancellationToken);
            var text = completion.Value.Content?.Count > 0 ? completion.Value.Content[0].Text : null;

            if (string.IsNullOrWhiteSpace(text))
                return (new List<SpellingIssue>(), null, "Không đọc được kết quả kiểm tra chính tả từ AI.");

            var (chunkIssues, chunkSummary) = ParseSpellingResponse(text, content);
            allIssues.AddRange(chunkIssues);
            if (!string.IsNullOrWhiteSpace(chunkSummary))
                chunkSummaries.Add(chunkSummary.Trim());
        }

        var deduped = DeduplicateIssues(allIssues);
        var finalSummary = BuildMergedSpellSummary(deduped.Count, chunks.Count, chunkSummaries);

        merged = new SpellCheckMerged { Issues = deduped, Summary = finalSummary };
        _cache.Set(cacheKey, merged, SpellCacheTtl);
        return (deduped, finalSummary, null);
    }

    private static string BuildSpellCheckUserPrompt(string? chapterTitle, string chunkBody, int chunkIndex, int chunkCount)
    {
        var titlePart = string.IsNullOrWhiteSpace(chapterTitle) ? "" : $"Tiêu đề chương: {chapterTitle}\n\n";
        var scopeNote = chunkCount > 1
            ? $"(Đoạn {chunkIndex}/{chunkCount} — chỉ báo lỗi chính tả trong đoạn dưới đây, không suy diễn ngoài đoạn.)\n\n"
            : "";

        return $@"{titlePart}{scopeNote}Nội dung chương cần kiểm tra chính tả:

---
{chunkBody}
---

Nhiệm vụ: CHỈ tìm lỗi chính tả/đánh máy (typo) trong đoạn trên (tiếng Việt hoặc tiếng Anh).

RÀNG BUỘC BẮT BUỘC:
- Chỉ ghi nhận khi chắc chắn là typo. Nếu không chắc chắn: bỏ qua.
- Tuyệt đối không gợi ý thay đổi văn phong, ngữ nghĩa, đại từ, hoặc “trau chuốt” câu chữ.
- Không paraphrase, không biên tập, không thay từ đúng bằng từ khác.
- Không bịa lỗi.

Trả về DUY NHẤT một JSON hợp lệ, không markdown hay giải thích:
{{ ""spellingErrors"": [ {{ ""wordOrPhrase"": ""từ/cụm sai"", ""suggestion"": ""gợi ý sửa"", ""context"": ""một câu (hoặc một dòng đối thoại) copy NGUYÊN VĂN từ nội dung phía trên, phải chứa đúng từ/cụm sai"" }} ], ""summary"": ""Tóm tắt ngắn cho tác giả (1-2 câu)"" }}

BẮT BUỘC: với mỗi lỗi trong spellingErrors, ""context"" phải là đoạn copy từ nội dung gốc (không tự viết lại), và phải chứa đúng ""wordOrPhrase"".
Nếu không có lỗi chính tả (hoặc không chắc chắn): spellingErrors = [], summary = ""Không phát hiện lỗi chính tả.""";
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

        foreach (var s in chunkSummaries)
        {
            if (SummaryIndicatesSpellingIssue(s))
                return s;
        }

        return "Không phát hiện lỗi chính tả.";
    }

    /// <summary>Chia nội dung dài thành khối ≤ maxChars, ưu tiên cắt tại xuống dòng.</summary>
    private static IReadOnlyList<string> SplitIntoSpellChunks(string content, int maxChars)
    {
        if (maxChars < 800) maxChars = 800;
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
            start = splitAt;
            while (start < content.Length && content[start] == '\n') start++;
        }

        return list.Count > 0 ? list : new[] { content };
    }

    private static string GetSystemPrompt()
    {
        return """
Bạn là hệ thống kiểm tra chính tả (typo) cho nội dung chương truyện.

CHỈ được phép trả về các lỗi chính tả/đánh máy khi chắc chắn. Nếu không chắc chắn thì phải bỏ qua và trả danh sách rỗng.
TUYỆT ĐỐI CẤM: đổi văn phong, đổi ngữ nghĩa, đổi đại từ, biên tập câu, diễn giải lại, hoặc thay từ đúng bằng từ khác.

Đầu ra BẮT BUỘC: chỉ một JSON hợp lệ theo đúng schema:
{ "spellingErrors": [ { "wordOrPhrase": "...", "suggestion": "...", "context": "..." } ], "summary": "..." }
Với mỗi lỗi, "context" phải là một câu/dòng trích NGUYÊN VĂN từ nội dung chương và chứa đúng từ/cụm sai.
Không markdown. Không thêm text ngoài JSON.
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

                    if (!IsLikelyTypoCorrection(wordOrPhrase, suggestion) && !punctuationLike)
                        continue;
                    if (IsAcceptedVariantPair(wordOrPhrase, suggestion))
                        continue;

                    var needleForExtract = rawWordOrPhrase.Trim();
                    if (!punctuationLike &&
                        !string.IsNullOrEmpty(needleForExtract) &&
                        !IsPlaceholderTypoLabel(needleForExtract))
                    {
                        var ctxTrim = (context ?? "").Trim();
                        if (string.IsNullOrEmpty(ctxTrim) ||
                            !ctxTrim.Contains(needleForExtract, StringComparison.OrdinalIgnoreCase))
                        {
                            var extracted = TryExtractContextSnippet(chapterContent, needleForExtract);
                            if (!string.IsNullOrEmpty(extracted))
                                context = extracted;
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
            {
                dedup.Add(new SpellingIssue
                {
                    WordOrPhrase = "Lỗi chính tả/dấu câu",
                    Suggestion = "Tóm tắt có nêu lỗi chính tả nhưng không trích được câu chứa từ sai trong nội dung. Vui lòng đọc phần tóm tắt và rà lại toàn đoạn.",
                    Context = summary
                });
            }
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

    /// <summary>Trích dòng (hoặc đoạn ngắn) chứa <paramref name="needle"/> từ nội dung gốc để hiển thị thay cho tọa độ ký tự.</summary>
    private static string? TryExtractContextSnippet(string chapterContent, string needle)
    {
        if (string.IsNullOrWhiteSpace(chapterContent) || string.IsNullOrWhiteSpace(needle)) return null;
        needle = needle.Trim();
        if (needle.Length == 0) return null;

        var idx = chapterContent.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;

        var lineStart = chapterContent.LastIndexOf('\n', idx);
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var lineEnd = chapterContent.IndexOf('\n', idx);
        if (lineEnd < 0) lineEnd = chapterContent.Length;
        var line = chapterContent[lineStart..lineEnd].Trim();
        if (line.Length == 0 || !line.Contains(needle, StringComparison.OrdinalIgnoreCase))
            return null;

        const int maxLen = 600;
        if (line.Length <= maxLen) return line;

        var rel = idx - lineStart;
        var half = maxLen / 2;
        var a = Math.Max(0, Math.Min(rel - half, line.Length - maxLen));
        var b = Math.Min(line.Length, a + maxLen);
        var snippet = line[a..b].Trim();
        return (a > 0 ? "… " : "") + snippet + (b < line.Length ? " …" : "");
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

    private static List<SpellingIssue> DeduplicateIssues(List<SpellingIssue> issues)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<SpellingIssue>();
        foreach (var i in issues)
        {
            var key = $"{i.WordOrPhrase?.Trim()}|{i.Suggestion?.Trim()}";
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
