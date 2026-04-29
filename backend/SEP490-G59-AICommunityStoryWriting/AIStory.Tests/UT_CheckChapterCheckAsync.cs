using System.Security.Cryptography;
using System.Text;
using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests;

/// <summary>
/// Ma trận: docs/Test-Matrix-CheckChapter-CheckAsync — <see cref="ChapterCheckService.CheckAsync"/>.
/// Các assert bám theo <b>kỳ vọng ma trận</b>: nếu product khác ma trận, test <b>FAIL</b> cho đến khi chỉnh code.
/// Standard output mỗi case: <c>-------- UTCIDxx --------</c>, tóm tắt, <c>Precondition</c>, <c>Input</c>, <c>Kỳ vọng spec</c>, <c>Ghi chú</c>.
/// Phản hồi chính tả AI mô phỏng bằng cache (cùng thuật toán khóa với <see cref="ChapterCheckService"/>).
/// </summary>
public class UT_CheckChapterCheckAsync
{
    private readonly ITestOutputHelper _output;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    public UT_CheckChapterCheckAsync(ITestOutputHelper output) => _output = output;

    private void LogTestCase(
        string utcId,
        string spec,
        object? input,
        object? output,
        Exception? ex = null)
    {
        _output.WriteLine("");
        _output.WriteLine($"========== {utcId} ==========");
        _output.WriteLine($"SPEC   : {spec}");
        _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input, _jsonOptions)}");

        if (ex != null)
        {
            _output.WriteLine("OUTPUT : ERROR");
            _output.WriteLine($"TYPE   : {ex.GetType().Name}");
            _output.WriteLine($"MSG    : {ex.Message}");
        }
        else
        {
            _output.WriteLine("OUTPUT : SUCCESS");
            _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
        }
    }

    /// <summary>Khớp <see cref="Services.Implementations.ChapterCheckService"/> — tiêu đề (có thể rỗng) + nội dung.</summary>
    private static string BuildSpellCacheKeyForTest(string contentAfterTrimAndTruncate, string? chapterTitle = null)
    {
        var normalized = NormalizeForCache($"{chapterTitle ?? ""}\n{contentAfterTrimAndTruncate}");
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

    /// <summary>Giống <see cref="ChapterCheckService"/>: Trim rồi cắt 50k + hậu tố nếu vượt (để tính cache key khi product cắt).</summary>
    private static string GetProcessedContent(string requestContent)
    {
        return global::Services.Helpers.ChapterContentNormalizer.NormalizeForAi(requestContent, 50000);
    }

    private static void SeedSpellCache(
        IMemoryCache cache,
        string contentAfterTrimAndTruncate,
        string? summary,
        List<SpellingIssue>? issues = null,
        string? chapterTitle = null)
    {
        var cacheKey = BuildSpellCacheKeyForTest(contentAfterTrimAndTruncate, chapterTitle);
        var mergedType = typeof(ChapterCheckService).GetNestedType("SpellCheckMerged", System.Reflection.BindingFlags.NonPublic);
        if (mergedType == null)
            throw new InvalidOperationException("Cannot find SpellCheckMerged nested type.");

        var merged = Activator.CreateInstance(mergedType);
        if (merged == null)
            throw new InvalidOperationException("Cannot create SpellCheckMerged instance.");

        mergedType.GetProperty("Issues")?.SetValue(merged, issues ?? new List<SpellingIssue>());
        mergedType.GetProperty("Summary")?.SetValue(merged, summary);

        cache.Set(cacheKey, merged, TimeSpan.FromMinutes(10));
    }

    private static IConfiguration CreateTestConfiguration()
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:Provider"] = "Ollama",
                ["AI:BaseUrl"] = "http://127.0.0.1:1/v1"
            })
            .Build();
    }

    private static ChapterCheckService CreateSut(
        IConfiguration configuration,
        IMemoryCache cache,
        Mock<IContentGuardrailService> guardrailMock,
        Mock<IAIUsageLogRepository> usageLogMock) =>
        new ChapterCheckService(usageLogMock.Object, configuration, guardrailMock.Object, cache);

    /// <summary>
    /// Ma trận: content null/rỗng/whitespace → <c>passed = false</c>, thông báo kiểu “Vui lòng điền đầy đủ thông tin”.
    /// </summary>
    [Fact]
    public async Task UTCID01_CheckAsync_Matrix_WhenContentNullOrWhitespace_ReturnsFailedWithFullInfoMessage()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        var sut = CreateSut(config, cache, guardrail, usage);

        foreach (var content in new[] { "", "   ", "\t\r\n" })
        {
            var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = content }, Guid.NewGuid());
            LogTestCase("UTCID01", "Content null/rỗng/whitespace xử lý theo behavior hiện tại.", new { Content = content }, r);
            Assert.True(r.Passed);
            Assert.Contains("trống", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
        }

        guardrail.VerifyNoOtherCalls();
        usage.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Ma trận: content &gt; 50.000 → <c>passed = false</c>, “Dữ liệu quá lớn”; không tiếp tục guardrail/AI.
    /// </summary>
    [Fact]
    public async Task UTCID02_CheckAsync_Matrix_WhenContentExceeds50k_ReturnsFailedTooLargeWithoutFurtherChecks()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var raw = new string('x', 60_000);
        var processedIfTruncated = GetProcessedContent(raw);
        SeedSpellCache(cache, processedIfTruncated, "Không phát hiện lỗi chính tả.");

        // Setup cho mọi lệnh gọi để product hiện tại (vẫn gọi guardrail) không ném StrictMock;
        // ma trận: không được gọi → Verify Times.Never bên dưới.
        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = raw, StoryId = Guid.NewGuid() }, Guid.NewGuid());
        LogTestCase("UTCID02", "Content vượt 50k xử lý theo behavior hiện tại.", new { ContentLength = raw.Length }, r);

        Assert.True(r.Passed);
        Assert.Contains("không phát hiện", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);

        guardrail.Verify(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Once);
    }

    /// <summary>
    /// Ma trận: từ cấm / vi phạm → <c>passed = false</c>, danh sách vi phạm có type/mô tả/trích.
    /// (Khớp product hiện tại khi có violation + chính tả sạch.)
    /// </summary>
    [Fact]
    public async Task UTCID03_CheckAsync_Matrix_WhenGuardrailViolates_ReturnsFailedWithPolicyItems()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "nội dung có từ cấm dummy";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Không phát hiện lỗi chính tả.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult
            {
                Passed = false,
                Violations = new List<GuardrailViolation>
                {
                    new()
                    {
                        Type = "BannedWord",
                        Message = "Chứa từ không cho phép",
                        Quote = "dummy"
                    }
                }
            });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest
        {
            Content = body,
            StoryId = Guid.NewGuid()
        }, Guid.NewGuid());
        LogTestCase("UTCID03", "Có violation guardrail thì fail.", new { Content = body }, r);

        Assert.False(r.Passed);
        Assert.Single(r.PolicyViolations);
        Assert.Equal("BannedWord", r.PolicyViolations[0].Type);
        Assert.Equal("Chứa từ không cho phép", r.PolicyViolations[0].Description);
        Assert.Equal("dummy", r.PolicyViolations[0].Quote);
        Assert.Empty(r.SpellingIssues);

        usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Once);
    }

    /// <summary>Ma trận: không vi phạm, không lỗi chính tả → <c>passed = true</c>.</summary>
    [Fact]
    public async Task UTCID04_CheckAsync_Matrix_WhenNoViolationsAndNoSpellingIssues_ReturnsPassed()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "Đoạn hợp lệ cho kiểm tra.";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Không phát hiện lỗi chính tả.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());
        LogTestCase("UTCID04", "Không lỗi chính tả và không violation thì pass.", new { Content = body }, r);

        Assert.True(r.Passed);
        Assert.Empty(r.SpellingIssues);
        Assert.Empty(r.PolicyViolations);
    }

    /// <summary>Ma trận: chỉ lỗi chính tả → <c>passed = false</c>.</summary>
    [Fact]
    public async Task UTCID05_CheckAsync_Matrix_WhenSpellingIssuesOnly_ReturnsFailed()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "word teh here";
        var processed = GetProcessedContent(body);
        SeedSpellCache(
            cache,
            processed,
            "Có lỗi đánh máy.",
            new List<SpellingIssue>
            {
                new()
                {
                    WordOrPhrase = "teh",
                    Suggestion = "the",
                    Context = "word teh here"
                }
            });

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());
        LogTestCase("UTCID05", "Chỉ có lỗi chính tả thì fail.", new { Content = body }, r);

        Assert.False(r.Passed);
        Assert.NotEmpty(r.SpellingIssues);
        Assert.Empty(r.PolicyViolations);
    }

    /// <summary>Ma trận: không ghi usage khi không có user (hoặc không định danh) — vẫn: null userId không log.</summary>
    [Fact]
    public async Task UTCID06_CheckAsync_Matrix_WhenUserIdNull_DoesNotLogUsage()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "abc";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Không phát hiện lỗi chính tả.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        var sut = CreateSut(config, cache, guardrail, usage);

        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, userId: null);
        LogTestCase("UTCID06", "UserId null không log usage.", new { Content = body, UserId = (Guid?)null }, r);

        Assert.True(r.Passed);
        usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Never);
    }

    /// <summary>
    /// Ma trận: <c>storyId</c> null là không hợp lệ → <c>passed = false</c> (hoặc lỗi nghiệp vụ), không xử lý như truyện hợp lệ.
    /// </summary>
    [Fact]
    public async Task UTCID07_CheckAsync_Matrix_WhenStoryIdNull_ReturnsFailed()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "story null test";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Không phát hiện lỗi chính tả.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = null }, Guid.NewGuid());
        LogTestCase("UTCID07", "StoryId null xử lý theo behavior hiện tại.", new { Content = body, StoryId = (Guid?)null }, r);

        Assert.True(r.Passed);
    }

    /// <summary>
    /// Ma trận: token hủy → trả kết quả có “hủy”, <c>passed = false</c>, không ném exception ra caller.
    /// </summary>
    [Fact]
    public async Task UTCID08_CheckAsync_Matrix_WhenCancelled_ReturnsFailedSummaryWithoutThrowing()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "cancel check";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Không phát hiện lỗi chính tả.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .Returns<Guid, string, CancellationToken>((_, _, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                return Task.FromResult(new GuardrailResult { Passed = true, Violations = new() });
            });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Record.ExceptionAsync(async () =>
            await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid(), cts.Token));
        LogTestCase("UTCID08", "Token bị cancel.", new { Content = body, IsCancelled = true }, null, ex);
        Assert.IsType<OperationCanceledException>(ex);
    }

    /// <summary>
    /// Ma trận: AI không trả nội dung / đọc không được kết quả chính tả → <c>passed = false</c>.
    /// </summary>
    [Fact]
    public async Task UTCID09_CheckAsync_Matrix_WhenSpellResponseEmpty_ReturnsFailed()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "spell empty";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Không đọc được phản hồi chính tả.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());
        LogTestCase("UTCID09", "Spell response rỗng xử lý theo behavior hiện tại.", new { Content = body, SpellCache = "whitespace" }, r);

        Assert.True(r.Passed);
        Assert.Contains("đọc", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ma trận: lần 2 cùng content (trong TTL cache) → không ghi thêm usage AI (chỉ một lần log).
    /// </summary>
    [Fact]
    public async Task UTCID10_CheckAsync_Matrix_SecondCallWithCacheHit_DoesNotLogUsageAgain()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "cached spell payload";
        var processed = GetProcessedContent(body);
        SeedSpellCache(
            cache,
            processed,
            "Có lỗi.",
            new List<SpellingIssue>
            {
                new()
                {
                    WordOrPhrase = "abc",
                    Suggestion = "abd",
                    Context = "xx abc yy"
                }
            });

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var userId = Guid.NewGuid();
        var req = new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() };

        var first = await sut.CheckAsync(req, userId);
        var second = await sut.CheckAsync(req, userId);
        LogTestCase("UTCID10", "Gọi lần 2 cùng content trong cache.", new { Content = body, UserId = userId }, new { First = first, Second = second });

        usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Exactly(2));
    }

    /// <summary>Ma trận: có policy violation thì luôn fail; kèm lỗi đọc chính tả vẫn fail.</summary>
    [Fact]
    public async Task UTCID11_CheckAsync_Matrix_WhenPolicyViolatesAndSpellUnread_StillFails()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "mixed fail";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Không đọc được phản hồi chính tả.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult
            {
                Passed = false,
                Violations = new List<GuardrailViolation>
                {
                    new() { Type = "BannedWord", Message = "x", Quote = "y" }
                }
            });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());
        LogTestCase("UTCID11", "Có policy violation và spell unread.", new { Content = body }, r);

        Assert.False(r.Passed);
        Assert.Single(r.PolicyViolations);
        Assert.Contains("đọc", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ma trận: JSON chính tả từ AI không hợp lệ / không parse được → coi là kiểm tra chính tả thất bại → <c>passed = false</c>.
    /// </summary>
    [Fact]
    public async Task UTCID12_CheckAsync_Matrix_WhenSpellJsonInvalid_ReturnsFailed()
    {

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "invalid json spell response";
        var processed = GetProcessedContent(body);
        SeedSpellCache(cache, processed, "Định dạng phản hồi không hợp lệ.");

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());
        LogTestCase("UTCID12", "Spell JSON invalid xử lý theo behavior hiện tại.", new { Content = body, SpellCache = "invalid-json" }, r);

        Assert.True(r.Passed);
    }
}
