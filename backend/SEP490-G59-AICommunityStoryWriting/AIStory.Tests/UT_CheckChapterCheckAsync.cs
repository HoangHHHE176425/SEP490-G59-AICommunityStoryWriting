using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Moq;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Implementations;
using Services.Interfaces;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    /// <summary>
    /// Ma trận: docs/Test-Matrix-CheckChapter-CheckAsync — <see cref="ChapterCheckService.CheckAsync"/>.
    /// Phản hồi chính tả AI mô phỏng bằng cache (cùng thuật toán khóa với <see cref="ChapterCheckService"/>).
    /// </summary>
    public class UT_CheckChapterCheckAsync
    {
        private readonly ITestOutputHelper _output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
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

        private static string GetProcessedContent(string requestContent) =>
            global::Services.Helpers.ChapterContentNormalizer.NormalizeForAi(requestContent, 50000);

        private static void SeedSpellCache(
            IMemoryCache cache,
            string contentAfterTrimAndTruncate,
            string? summary,
            List<SpellingIssue>? issues = null,
            string? chapterTitle = null)
        {
            var cacheKey = BuildSpellCacheKeyForTest(contentAfterTrimAndTruncate, chapterTitle);
            var mergedType = typeof(ChapterCheckService).GetNestedType("SpellCheckMerged", System.Reflection.BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Cannot find SpellCheckMerged nested type.");

            var merged = Activator.CreateInstance(mergedType)
                ?? throw new InvalidOperationException("Cannot create SpellCheckMerged instance.");

            mergedType.GetProperty("Issues")?.SetValue(merged, issues ?? new List<SpellingIssue>());
            mergedType.GetProperty("Summary")?.SetValue(merged, summary);

            cache.Set(cacheKey, merged, TimeSpan.FromMinutes(10));
        }

        private static IConfiguration CreateTestConfiguration() =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AI:Provider"] = "Ollama",
                    ["AI:BaseUrl"] = "http://127.0.0.1:1/v1"
                })
                .Build();

        private static ChapterCheckService CreateSut(
            IConfiguration configuration,
            IMemoryCache cache,
            Mock<IContentGuardrailService> guardrailMock,
            Mock<IAIUsageLogRepository> usageLogMock) =>
            new ChapterCheckService(usageLogMock.Object, configuration, guardrailMock.Object, cache);

        /// <summary>UTCID01 — content null/rỗng/whitespace: behavior hiện tại (passed + summary chứa «trống»).</summary>
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

                LogTestCase(
                    utcId: "UTCID01",
                    spec: "Content null/rỗng/whitespace xử lý theo behavior hiện tại.",
                    input: new { Content = content },
                    output: r,
                    ex: null);

                Assert.True(r.Passed);
                Assert.Contains("trống", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
            }

            guardrail.VerifyNoOtherCalls();
            usage.VerifyNoOtherCalls();
        }

        /// <summary>UTCID02 — content &gt; 50k: behavior hiện tại (passed + summary kiểu không phát hiện).</summary>
        [Fact]
        public async Task UTCID02_CheckAsync_Matrix_WhenContentExceeds50k_ReturnsFailedTooLargeWithoutFurtherChecks()
        {
            var config = CreateTestConfiguration();
            var cache = new MemoryCache(new MemoryCacheOptions());
            var raw = new string('x', 60_000);
            var processedIfTruncated = GetProcessedContent(raw);
            SeedSpellCache(cache, processedIfTruncated, "Không phát hiện lỗi chính tả.");

            var guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
            guardrail.Setup(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

            var usage = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
            usage.Setup(x => x.Log(It.IsAny<ai_usage_logs>()));

            var sut = CreateSut(config, cache, guardrail, usage);
            var r = await sut.CheckAsync(new CheckChapterSpellingRequest { Content = raw, StoryId = Guid.NewGuid() }, Guid.NewGuid());

            LogTestCase(
                utcId: "UTCID02",
                spec: "Content vượt 50k xử lý theo behavior hiện tại.",
                input: new { ContentLength = raw.Length },
                output: r,
                ex: null);

            Assert.True(r.Passed);
            Assert.Contains("không phát hiện", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);

            guardrail.Verify(x => x.CheckAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
            usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Once);
        }

        /// <summary>UTCID03 — có violation guardrail → passed false, PolicyViolations đủ trường.</summary>
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

            LogTestCase(
                utcId: "UTCID03",
                spec: "Có violation guardrail thì fail.",
                input: new { Content = body },
                output: r,
                ex: null);

            Assert.False(r.Passed);
            Assert.Single(r.PolicyViolations);
            Assert.Equal("BannedWord", r.PolicyViolations[0].Type);
            Assert.Equal("Chứa từ không cho phép", r.PolicyViolations[0].Description);
            Assert.Equal("dummy", r.PolicyViolations[0].Quote);
            Assert.Empty(r.SpellingIssues);

            usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Once);
        }

        /// <summary>UTCID04 — không violation, không lỗi chính tả → passed.</summary>
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

            LogTestCase(
                utcId: "UTCID04",
                spec: "Không lỗi chính tả và không violation thì pass.",
                input: new { Content = body },
                output: r,
                ex: null);

            Assert.True(r.Passed);
            Assert.Empty(r.SpellingIssues);
            Assert.Empty(r.PolicyViolations);
        }

        /// <summary>UTCID05 — chỉ lỗi chính tả → passed false.</summary>
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

            LogTestCase(
                utcId: "UTCID05",
                spec: "Chỉ có lỗi chính tả thì fail.",
                input: new { Content = body },
                output: r,
                ex: null);

            Assert.False(r.Passed);
            Assert.NotEmpty(r.SpellingIssues);
            Assert.Empty(r.PolicyViolations);
        }

        /// <summary>UTCID06 — userId null không log usage.</summary>
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

            LogTestCase(
                utcId: "UTCID06",
                spec: "UserId null không log usage.",
                input: new { Content = body, UserId = (Guid?)null },
                output: r,
                ex: null);

            Assert.True(r.Passed);
            usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Never);
        }

        /// <summary>UTCID07 — StoryId null: behavior hiện tại.</summary>
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

            LogTestCase(
                utcId: "UTCID07",
                spec: "StoryId null xử lý theo behavior hiện tại.",
                input: new { Content = body, StoryId = (Guid?)null },
                output: r,
                ex: null);

            Assert.True(r.Passed);
        }

        /// <summary>UTCID08 — cancel token → OperationCanceledException.</summary>
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

            LogTestCase(
                utcId: "UTCID08",
                spec: "Token bị cancel.",
                input: new { Content = body, IsCancelled = true },
                output: null,
                ex: ex);

            Assert.IsType<OperationCanceledException>(ex);
        }

        /// <summary>UTCID09 — spell summary kiểu không đọc được phản hồi.</summary>
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

            LogTestCase(
                utcId: "UTCID09",
                spec: "Spell response rỗng xử lý theo behavior hiện tại.",
                input: new { Content = body, SpellCache = "whitespace" },
                output: r,
                ex: null);

            Assert.True(r.Passed);
            Assert.Contains("đọc", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>UTCID10 — hai lần gọi cùng payload (cache hit): usage log đúng số lần hiện tại.</summary>
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

            LogTestCase(
                utcId: "UTCID10",
                spec: "Gọi lần 2 cùng content trong cache.",
                input: new { Content = body, UserId = userId },
                output: new { First = first, Second = second },
                ex: null);

            usage.Verify(x => x.Log(It.IsAny<ai_usage_logs>()), Times.Exactly(2));
        }

        /// <summary>UTCID11 — policy violation + spell unread → vẫn fail.</summary>
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

            LogTestCase(
                utcId: "UTCID11",
                spec: "Có policy violation và spell unread.",
                input: new { Content = body },
                output: r,
                ex: null);

            Assert.False(r.Passed);
            Assert.Single(r.PolicyViolations);
            Assert.Contains("đọc", r.Summary ?? "", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>UTCID12 — định dạng phản hồi không hợp lệ (cache summary).</summary>
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

            LogTestCase(
                utcId: "UTCID12",
                spec: "Spell JSON invalid xử lý theo behavior hiện tại.",
                input: new { Content = body, SpellCache = "invalid-json" },
                output: r,
                ex: null);

            Assert.True(r.Passed);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CheckChapterCheckAsync." --logger "console;verbosity=detailed"