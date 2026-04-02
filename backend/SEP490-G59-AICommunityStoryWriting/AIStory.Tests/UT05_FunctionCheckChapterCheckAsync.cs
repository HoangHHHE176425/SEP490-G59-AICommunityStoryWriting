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
using Xunit.Abstractions;

namespace AIStory.Tests;

/// <summary>
/// Ma trận: docs/Test-Matrix-CheckChapter-CheckAsync — <see cref="ChapterCheckService.CheckAsync"/>.
/// Các assert bám theo <b>kỳ vọng ma trận</b>: nếu product khác ma trận, test <b>FAIL</b> cho đến khi chỉnh code.
/// Standard output mỗi case: <c>-------- UTCIDxx --------</c>, tóm tắt, <c>Precondition</c>, <c>Input</c>, <c>Kỳ vọng spec</c>, <c>Ghi chú</c>.
/// Phản hồi chính tả AI mô phỏng bằng cache (cùng thuật toán khóa với <see cref="ChapterCheckService"/>).
/// </summary>
public class UT05_FunctionCheckChapterCheckAsync
{
    private readonly ITestOutputHelper _output;

    public UT05_FunctionCheckChapterCheckAsync(ITestOutputHelper output) => _output = output;

    /// <summary>
    /// Ghi ra đúng format “Standard Output Messages” (giống UT chapter: -------- UTCIDxx --------, Precondition, Input, Kỳ vọng spec, Ghi chú).
    /// </summary>
    private void LogMatrixCase(
        string utcId,
        string summary,
        string precondition,
        string input,
        string specExpectation,
        string? productNote = null)
    {
        _output.WriteLine("");
        _output.WriteLine($"-------- {utcId} --------");
        _output.WriteLine(summary);
        _output.WriteLine($"Precondition: {precondition}");
        _output.WriteLine($"Input: {input}");
        _output.WriteLine($"Kỳ vọng spec: {specExpectation}");
        _output.WriteLine(string.IsNullOrEmpty(productNote) ? "Ghi chú: (xem assert / stack trace nếu fail)." : $"Ghi chú: {productNote}");
    }

    private static string BuildSpellCacheKeyForTest(string contentAfterTrimAndTruncate)
    {
        var normalized = NormalizeForCache(contentAfterTrimAndTruncate);
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
        var content = requestContent.Trim();
        if (content.Length > 50000)
            content = content[..50000] + "\n[... nội dung bị cắt bớt ...]";
        return content;
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
        LogMatrixCase("UTCID01",
            "Nội dung null/rỗng/chỉ whitespace — ma trận yêu cầu coi là không hợp lệ: passed=false và thông báo thiếu thông tin (điền đầy đủ).",
            "ChapterCheckService; mock Strict guardrail + usage (không được gọi khi fail sớm theo spec).",
            "CheckAsync(CheckChapterSpellingRequest với Content = \"\", \"   \", \"\\t\\r\\n\"; userId có giá trị).",
            "passed=false; Summary chứa \"đầy đủ\"; không gọi guardrail; không Log usage.",
            "Product hiện trả passed=true, Summary \"Nội dung trống, không cần kiểm tra.\" — lệch ma trận → test FAIL tới khi chỉnh CheckAsync.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        var sut = CreateSut(config, cache, guardrail, usage);

        foreach (var content in new[] { "", "   ", "\t\r\n" })
        {
            var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = content }, Guid.NewGuid());
            Assert.False(r.Passed);
            Assert.Contains("đầy đủ", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
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
        LogMatrixCase("UTCID02",
            "Content > 50.000 ký tự — ma trận yêu cầu fail \"Dữ liệu quá lớn\", không chạy guardrail / chính tả / ghi usage.",
            "Config AI in-memory; cache seed JSON chính tả cho bản sau cắt (tránh treo nếu product vẫn đi áp cắt); mock guardrail có setup mọi lệnh gọi.",
            "CheckAsync(Content = 60.000 ký tự 'x'; StoryId và userId hợp lệ).",
            "passed=false; Summary có \"lớn\"; Verify guardrail và Log đều Times.Never.",
            "Product hiện cắt 50k + hậu tố, gọi guardrail và chính tả, Log usage — test FAIL tới khi chỉnh CheckAsync.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var raw = new string('x', 60_000);
        var processedIfTruncated = GetProcessedContent(raw);
        cache.Set(BuildSpellCacheKeyForTest(processedIfTruncated),
            """{"spellingErrors":[],"summary":"Không phát hiện lỗi chính tả."}""",
            TimeSpan.FromMinutes(10));

        // Setup cho mọi lệnh gọi để product hiện tại (vẫn gọi guardrail) không ném StrictMock;
        // ma trận: không được gọi → Verify Times.Never bên dưới.
        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = raw, StoryId = Guid.NewGuid() }, Guid.NewGuid());

        Assert.False(r.Passed);
        Assert.Contains("lớn", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);

        guardrail.Verify(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Never);
    }

    /// <summary>
    /// Ma trận: từ cấm / vi phạm → <c>passed = false</c>, danh sách vi phạm có type/mô tả/trích.
    /// (Khớp product hiện tại khi có violation + chính tả sạch.)
    /// </summary>
    [Fact]
    public async Task UTCID03_CheckAsync_Matrix_WhenGuardrailViolates_ReturnsFailedWithPolicyItems()
    {
        LogMatrixCase("UTCID03",
            "Có vi phạm từ cấm (guardrail) và chính tả sạch — ma trận yêu cầu passed=false và danh sách vi phạm đủ type/mô tả/trích.",
            "Cache trả JSON spellingErrors rỗng; mock guardrail trả 1 violation BannedWord.",
            "CheckAsync(Content có body + StoryId; userId có).",
            "passed=false; PolicyViolations.Count=1 (BannedWord, Message, Quote); SpellingIssues rỗng; Log usage 1 lần.",
            "Product hiện map GuardrailViolation → PolicyViolationItem và passed phụ thuộc chính tả — khớp case này.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "nội dung có từ cấm dummy";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed),
            """{"spellingErrors":[],"summary":"Không phát hiện lỗi chính tả."}""",
            TimeSpan.FromMinutes(10));

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
        LogMatrixCase("UTCID04",
            "Guardrail sạch và không lỗi chính tả — ma trận yêu cầu passed=true.",
            "Cache JSON spellingErrors []; mock guardrail Passed.",
            "CheckAsync(Content ngắn hợp lệ + StoryId; userId có).",
            "passed=true; SpellingIssues và PolicyViolations rỗng.",
            "Product hiện khớp (passed = không lỗi chính tả và không violation).");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "Đoạn hợp lệ cho kiểm tra.";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed),
            """{"spellingErrors":[],"summary":"Không phát hiện lỗi chính tả."}""",
            TimeSpan.FromMinutes(10));

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());

        Assert.True(r.Passed);
        Assert.Empty(r.SpellingIssues);
        Assert.Empty(r.PolicyViolations);
    }

    /// <summary>Ma trận: chỉ lỗi chính tả → <c>passed = false</c>.</summary>
    [Fact]
    public async Task UTCID05_CheckAsync_Matrix_WhenSpellingIssuesOnly_ReturnsFailed()
    {
        LogMatrixCase("UTCID05",
            "Chỉ lỗi chính tả (không violation) — ma trận yêu cầu passed=false và có SpellingIssues.",
            "Cache JSON có spellingErrors hợp lệ (typo teh→the); mock guardrail sạch.",
            "CheckAsync(Content tiếng Anh có typo; userId có).",
            "passed=false; SpellingIssues không rỗng; PolicyViolations rỗng.",
            "Product hiện khớp.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "word teh here";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed),
            """
            {"spellingErrors":[{"wordOrPhrase":"teh","suggestion":"the","context":"word teh here"}],"summary":"Có lỗi đánh máy."}
            """,
            TimeSpan.FromMinutes(10));

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());

        Assert.False(r.Passed);
        Assert.NotEmpty(r.SpellingIssues);
        Assert.Empty(r.PolicyViolations);
    }

    /// <summary>Ma trận: không ghi usage khi không có user (hoặc không định danh) — vẫn: null userId không log.</summary>
    [Fact]
    public async Task UTCID06_CheckAsync_Matrix_WhenUserIdNull_DoesNotLogUsage()
    {
        LogMatrixCase("UTCID06",
            "userId null — ma trận yêu cầu không ghi AI usage (không Log).",
            "Cache + mock guardrail sạch.",
            "CheckAsync(Content; userId: null).",
            "passed=true (nội dung hợp lệ); IAIUsageLogRepository.Log Times.Never.",
            "Product hiện chỉ Log khi userId.HasValue — khớp.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "abc";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed),
            """{"spellingErrors":[],"summary":"Không phát hiện lỗi chính tả."}""",
            TimeSpan.FromMinutes(10));

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        var sut = CreateSut(config, cache, guardrail, usage);

        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, userId: null);

        Assert.True(r.Passed);
        usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Never);
    }

    /// <summary>
    /// Ma trận: <c>storyId</c> null là không hợp lệ → <c>passed = false</c> (hoặc lỗi nghiệp vụ), không xử lý như truyện hợp lệ.
    /// </summary>
    [Fact]
    public async Task UTCID07_CheckAsync_Matrix_WhenStoryIdNull_ReturnsFailed()
    {
        LogMatrixCase("UTCID07",
            "StoryId null — ma trận yêu cầu coi không hợp lệ: passed=false và thông báo liên quan story_id.",
            "Cache + mock guardrail (It.IsAny Guid).",
            "CheckAsync(Content; StoryId=null; userId có).",
            "passed=false; Summary chứa \"story\".",
            "Product hiện dùng StoryId ?? Guid.Empty và thường passed=true — test FAIL tới khi chỉnh validation.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "story null test";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed),
            """{"spellingErrors":[],"summary":"Không phát hiện lỗi chính tả."}""",
            TimeSpan.FromMinutes(10));

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = null }, Guid.NewGuid());

        Assert.False(r.Passed);
        Assert.Contains("story", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ma trận: token hủy → trả kết quả có “hủy”, <c>passed = false</c>, không ném exception ra caller.
    /// </summary>
    [Fact]
    public async Task UTCID08_CheckAsync_Matrix_WhenCancelled_ReturnsFailedSummaryWithoutThrowing()
    {
        LogMatrixCase("UTCID08",
            "CancellationToken đã hủy — ma trận yêu cầu trả CheckChapterResponse (passed=false, Summary có \"hủy\"), không ném exception.",
            "Cache + mock guardrail ThrowIfCancellationRequested khi được await.",
            "CheckAsync(Content; userId có; token đã Cancel()).",
            "Không throw OperationCanceledException; passed=false; Summary có \"hủy\".",
            "Product hiện để cancellation lan truyền (throw) từ await guardrail — test FAIL tới khi bắt token và trả response.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "cancel check";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed),
            """{"spellingErrors":[],"summary":"Không phát hiện lỗi chính tả."}""",
            TimeSpan.FromMinutes(10));

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

        CheckChapterResponse? r = null;
        try
        {
            r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid(), cts.Token);
        }
        catch (OperationCanceledException)
        {
            Assert.Fail("Ma trận: khi hủy phải trả CheckChapterResponse (Passed=false, tóm tắt hủy), không throw OperationCanceledException.");
        }

        Assert.NotNull(r);
        Assert.False(r.Passed);
        Assert.Contains("hủy", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ma trận: AI không trả nội dung / đọc không được kết quả chính tả → <c>passed = false</c>.
    /// </summary>
    [Fact]
    public async Task UTCID09_CheckAsync_Matrix_WhenSpellResponseEmpty_ReturnsFailed()
    {
        LogMatrixCase("UTCID09",
            "Phản hồi chính tả từ AI rỗng/không đọc được — ma trận yêu cầu passed=false (lỗi đọc kết quả).",
            "Cache giá trị whitespace; mock guardrail sạch.",
            "CheckAsync(Content; userId có).",
            "passed=false; Summary chứa \"đọc\".",
            "Product hiện đặt passed = (policyViolations.Count==0) khi spellRawError — có thể passed=true — test FAIL tới khi đổi rule.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "spell empty";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed), "   \n  ", TimeSpan.FromMinutes(10));

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());

        Assert.False(r.Passed);
        Assert.Contains("đọc", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Ma trận: lần 2 cùng content (trong TTL cache) → không ghi thêm usage AI (chỉ một lần log).
    /// </summary>
    [Fact]
    public async Task UTCID10_CheckAsync_Matrix_SecondCallWithCacheHit_DoesNotLogUsageAgain()
    {
        LogMatrixCase("UTCID10",
            "Hai lần CheckAsync giống content (cache chính tả hit lần 2) — ma trận yêu cầu chỉ 1 lần Log usage.",
            "Cache có sẵn JSON chính tả; mock guardrail; cùng userId hai lần.",
            "CheckAsync(req x2; userId cố định).",
            "IAIUsageLogRepository.Log Times.Once.",
            "Product hiện Log sau mỗi lần hoàn thành CheckAsync — thường 2 lần — test FAIL tới khi skip log khi spell cache hit.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "cached spell payload";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed),
            """
            {"spellingErrors":[{"wordOrPhrase":"abc","suggestion":"abd","context":"xx abc yy"}],"summary":"Có lỗi."}
            """,
            TimeSpan.FromMinutes(10));

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var userId = Guid.NewGuid();
        var req = new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() };

        _ = await sut.CheckAsync(req, userId);
        _ = await sut.CheckAsync(req, userId);

        usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Once);
    }

    /// <summary>Ma trận: có policy violation thì luôn fail; kèm lỗi đọc chính tả vẫn fail.</summary>
    [Fact]
    public async Task UTCID11_CheckAsync_Matrix_WhenPolicyViolatesAndSpellUnread_StillFails()
    {
        LogMatrixCase("UTCID11",
            "Có policy violation và không đọc được kết quả chính tả — ma trận yêu cầu passed=false và Summary lỗi đọc.",
            "Cache entry rỗng cho spelling; mock guardrail vi phạm.",
            "CheckAsync(Content; userId có).",
            "passed=false; có PolicyViolations; Summary chứa \"đọc\".",
            "Product hiện nhánh spellRawError với passed phụ thuộc policy — khớp case có violation.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "mixed fail";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed), "", TimeSpan.FromMinutes(10));

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
        LogMatrixCase("UTCID12",
            "Phản hồi không phải JSON hợp lệ cho chính tả — ma trận yêu cầu passed=false (check chính tả thất bại).",
            "Cache chuỗi garbage; mock guardrail sạch.",
            "CheckAsync(Content; userId có).",
            "passed=false.",
            "Product hiện ParseSpellingResponse catch: 0 issue nhưng có thể vẫn passed=true nếu không violation — test FAIL tới khi coi parse lỗi là fail.");

        var config = CreateTestConfiguration();
        var cache = new MemoryCache(new MemoryCacheOptions());
        const string body = "invalid json spell response";
        var processed = GetProcessedContent(body);
        cache.Set(BuildSpellCacheKeyForTest(processed), "{ not json at all [[[", TimeSpan.FromMinutes(10));

        var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
        guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), processed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

        var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
        usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

        var sut = CreateSut(config, cache, guardrail, usage);
        var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = body, StoryId = Guid.NewGuid() }, Guid.NewGuid());

        Assert.False(r.Passed);
    }
}
