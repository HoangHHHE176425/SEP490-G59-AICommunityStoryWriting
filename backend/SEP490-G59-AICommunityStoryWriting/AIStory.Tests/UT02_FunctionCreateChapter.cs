using BusinessObjects.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Repositories;
using Services.DTOs.Chapters;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT02_FunctionCreateChapter
    {
        private readonly ITestOutputHelper _output;

        public UT02_FunctionCreateChapter(ITestOutputHelper output) => _output = output;

        /// <summary>Ghi log có cấu trúc cho từng UTCID (hiển thị trong Test Explorer / dotnet test --logger).</summary>
        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
        }

        private static string JsonProbe(string? s) => s == null ? "(null)" : JsonSerializer.Serialize(s);

        private static ChapterService CreateSut(
            stories story,
            List<chapters> chapterStore,
            out Mock<IChapterRepository> chapterRepoMock,
            out Mock<IStoryLookup> storyLookupMock,
            out Mock<IUserLookup> userLookupMock,
            out Mock<IChapterVersionRepository> versionRepoMock,
            out Mock<IAiGeneratedContentRepository> aiRepoMock)
        {
            chapterRepoMock = new Mock<IChapterRepository>(MockBehavior.Strict);
            storyLookupMock = new Mock<IStoryLookup>(MockBehavior.Strict);
            userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            versionRepoMock = new Mock<IChapterVersionRepository>(MockBehavior.Strict);
            aiRepoMock = new Mock<IAiGeneratedContentRepository>(MockBehavior.Strict);

            // Story lookup
            storyLookupMock.Setup(x => x.GetById(story.id)).Returns(story);
            storyLookupMock.Setup(x => x.Update(It.IsAny<stories>()));

            // User lookup
            userLookupMock.Setup(x => x.Exists(It.IsAny<Guid>())).Returns(true);
            userLookupMock.Setup(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>())).Returns(false);

            // AI repo no-ops used by Create()
            aiRepoMock.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((ai_generated_content?)null);
            aiRepoMock.Setup(x => x.BindDraftChapterId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()));
            aiRepoMock.Setup(x => x.UpdateChapterId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()));

            // Chapter versions are queried in other methods; Create() doesn't need them, but keep safe defaults
            versionRepoMock.Setup(x => x.GetByChapterId(It.IsAny<Guid>())).Returns(Array.Empty<chapter_versions>());

            // Chapter repo: implement as in-memory store but verify via Moq
            chapterRepoMock.Setup(x => x.GetAll()).Returns(() => chapterStore.AsQueryable());
            chapterRepoMock.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((Guid id) => chapterStore.FirstOrDefault(c => c.id == id));
            chapterRepoMock.Setup(x => x.GetByStoryId(It.IsAny<Guid>())).Returns((Guid sid) => chapterStore.Where(c => c.story_id == sid).ToList());
            chapterRepoMock.Setup(x => x.GetByStoryIdAndOrderIndex(It.IsAny<Guid>(), It.IsAny<int>()))
                .Returns((Guid sid, int idx) => chapterStore.FirstOrDefault(c => c.story_id == sid && c.order_index == idx));
            chapterRepoMock.Setup(x => x.Add(It.IsAny<chapters>()))
                .Callback((chapters c) => chapterStore.Add(c));
            chapterRepoMock.Setup(x => x.Update(It.IsAny<chapters>()));
            chapterRepoMock.Setup(x => x.Delete(It.IsAny<Guid>()));
            chapterRepoMock.Setup(x => x.DeleteByStoryId(It.IsAny<Guid>()));

            return new ChapterService(
                chapterRepoMock.Object,
                versionRepoMock.Object,
                aiRepoMock.Object,
                userLookupMock.Object,
                storyLookupMock.Object,
                NullLogger<ChapterService>.Instance);
        }

        [Fact]
        public void UTCID01_CreateChapter_Fail_WhenAuthorIsNotOwner()
        {
            LogUtcContext("UTCID01",
                "Từ chối khi authorId gọi API không phải chủ story (ownership).",
                "Precondition: story tồn tại; user/story hợp lệ; order_index chưa trùng; title/content/access hợp lệ.",
                "Input: Create(req, otherAuthorId) với otherAuthorId != story.author_id.",
                "Kỳ vọng spec: exception + không Add chapter.");

            var authorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = authorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var content = new string('a', 500); // >= 500 ký tự
            var chapterId = Guid.NewGuid();

            var req = new CreateChapterRequestDto
            {
                Id = chapterId,
                StoryId = storyId,
                Title = "Chương 1",
                Content = content,
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 0
            };

            // Use a DIFFERENT author than the story owner -> must be rejected by ownership validation.
            var otherAuthorId = Guid.NewGuid();
            var ex = Assert.Throws<UnauthorizedAccessException>(() => sut.Create(req, otherAuthorId));
            Assert.Equal("Bạn không phải tác giả của truyện này.", ex.Message);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID02_CreateChapterPaid_Fail_WhenContentLengthLessThan500()
        {
            LogUtcContext("UTCID02",
                "PAID chapter: nội dung < 500 ký tự phải bị từ chối.",
                "Precondition: story tồn tại; author đúng; total_views >= 500 (rule PAID hiện tại); order_index chưa trùng.",
                "Input: Content length = 499; AccessType=PAID; CoinPrice=10.",
                "Kỳ vọng spec: exception + không Add.",
                "Ghi chú: product có thể chưa validate độ dài tối thiểu.");

            // Expected by spec, currently FAILS until product bug is fixed:
            // Create must validate content length >= 500 characters.
            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000 // must be >= 500 to allow PAID chapters by current product rules
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            // Intentionally violate spec
            var content = new string('b', 499);
            var chapterId = Guid.NewGuid();

            var req = new CreateChapterRequestDto
            {
                Id = chapterId,
                StoryId = storyId,
                Title = "Chương 1",
                Content = content,
                OrderIndex = 1,
                AccessType = "PAID",
                CoinPrice = 10
            };

            Assert.Throws<InvalidOperationException>(() => sut.Create(req, ownerAuthorId));
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID03_CreateChapter_Fail_WhenOrderIndexAlreadyExists()
        {
            LogUtcContext("UTCID03",
                "Trùng order_index cho cùng story → không tạo chapter thứ hai.",
                "Precondition: story tồn tại; đã có chapter cùng story_id + order_index.",
                "Input: OrderIndex trùng với bản ghi đã seed trong store.",
                "Kỳ vọng: InvalidOperationException + Verify(Add, Never) + store vẫn 1 chapter.");

            // Spec:
            // - Story exists
            // - order_index already exists (duplicate)
            // - Expect: fail (no new data) + error message "Chương đã tồn tại"
            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>
            {
                new chapters
                {
                    id = Guid.NewGuid(),
                    story_id = storyId,
                    title = "Chương 1",
                    order_index = 1,
                    status = "DRAFT"
                }
            };
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = new string('c', 500),
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 0
            };

            Assert.Throws<InvalidOperationException>(() => sut.Create(req, ownerAuthorId));

            // Ensure no new chapter created
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            Assert.Single(chapterStore);
        }

        [Fact]
        public void UTCID04_CreateChapter_Fail_WhenAccessTypeInvalid()
        {
            LogUtcContext("UTCID04",
                "accessType không thuộc FREE/PAID → ArgumentException.",
                "Precondition: story + author hợp lệ; order_index chưa trùng.",
                "Input: AccessType = \"VIP\" (invalid).",
                "Kỳ vọng: ArgumentException + không Add.");

            // Spec:
            // - Story exists
            // - order_index not exists
            // - accessType invalid (not FREE/PAID)
            // - Expect: fail (no new data)
            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = new string('d', 500),
                OrderIndex = 1,
                AccessType = "VIP",
                CoinPrice = 0
            };

            Assert.Throws<ArgumentException>(() => sut.Create(req, ownerAuthorId));
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            Assert.Empty(chapterStore);
        }

        [Fact]
        public void UTCID05_CreateChapterPaid_Fail_WhenCoinPriceNegative()
        {
            LogUtcContext("UTCID05",
                "PAID nhưng coinPrice âm → ArgumentException.",
                "Precondition: story; total_views >= 500; order_index chưa trùng.",
                "Input: AccessType=PAID; CoinPrice = -10.",
                "Kỳ vọng: ArgumentException + không Add.");

            // Spec:
            // - Story exists
            // - order_index not exists
            // - accessType = PAID
            // - coinPrice = -10 (invalid, must be > 0)
            // - Expect: fail + no insert

            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = new string('e', 500),
                OrderIndex = 1,
                AccessType = "PAID",
                CoinPrice = -10
            };

            Assert.Throws<ArgumentException>(() => sut.Create(req, ownerAuthorId));

            // Verify: no insert into repository (equivalent to no SaveChanges in this abstraction)
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            Assert.Empty(chapterStore);
        }

        [Fact]
        public void UTCID06_CreateChapter_Fail_WhenFreeButCoinPricePositive()
        {
            LogUtcContext("UTCID06",
                "FREE nhưng coinPrice > 0 → spec yêu cầu fail (không được âm thầm ép về 0 rồi tạo).",
                "Precondition: story + author hợp lệ; order_index chưa trùng.",
                "Input: AccessType=FREE; CoinPrice=10.",
                "Kỳ vọng spec: ArgumentException + không Add.");

            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = new string('f', 500),
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 10
            };

            Assert.Throws<ArgumentException>(() => sut.Create(req, ownerAuthorId));
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        /// <summary>
        /// UTCID07 – title null hoặc chỉ khoảng trắng: spec yêu cầu fail, không tạo chapter.
        /// Product: <see cref="ChapterService.Create"/> từ chối qua <c>ArgumentException</c> trước khi gọi EnsureUniqueChapterTitleForStory.
        /// </summary>
        [Fact]
        public void UTCID07_CreateChapter_Fail_WhenTitleMissingOrWhitespace()
        {
            LogUtcContext("UTCID07",
                "Title null hoặc chỉ whitespace → spec: fail, không tạo chapter (2 biến thể trong một Fact).",
                "Precondition: story + author hợp lệ; content >= 500; order_index chưa trùng.",
                "Kỳ vọng spec: exception + không Add (mỗi vòng foreach).",
                "Ghi chú: product có thể không throw (EnsureUnique... return sớm).");

            foreach (var title in new string?[] { null, "   " })
            {
                _output.WriteLine($"  — UTCID07 iteration: Title = {JsonProbe(title)}");
                var ownerAuthorId = Guid.NewGuid();
                var storyId = Guid.NewGuid();
                var story = new stories
                {
                    id = storyId,
                    title = "Truyen A",
                    author_id = ownerAuthorId,
                    story_progress_status = "ONGOING",
                    total_views = 1000
                };

                var chapterStore = new List<chapters>();
                var sut = CreateSut(story, chapterStore,
                    out var chapterRepoMock,
                    out _,
                    out _,
                    out _,
                    out _);

                var req = new CreateChapterRequestDto
                {
                    Id = Guid.NewGuid(),
                    StoryId = storyId,
                    Title = title!,
                    Content = new string('g', 500),
                    OrderIndex = 1,
                    AccessType = "FREE",
                    CoinPrice = 0
                };

                Assert.Throws<ArgumentException>(() => sut.Create(req, ownerAuthorId));
                chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            }
        }

        /// <summary>
        /// UTCID08 – content chỉ khoảng trắng: spec yêu cầu fail, không tạo chapter; message nghiệp vụ (vd. đầy đủ thông tin).
        /// </summary>
        [Fact]
        public void UTCID08_CreateChapter_Fail_WhenContentIsWhitespaceOnly()
        {
            LogUtcContext("UTCID08",
                "Content chỉ khoảng trắng → spec: fail, không tạo chapter.",
                "Precondition: story + author; title hợp lệ; order_index chưa trùng; FREE/coinPrice=0.",
                "Input: Content = whitespace-only (tab/space/newline).",
                "Kỳ vọng spec: InvalidOperationException + không Add.");

            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = "   \t  \n  ",
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 0
            };

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(req, ownerAuthorId));
            Assert.Contains("Vui lòng điền đầy đủ thông tin", ex.Message, StringComparison.Ordinal);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        /// <summary>
        /// UTCID09 – content &lt; 500 ký tự (có ký tự thực, không phải chỉ whitespace): fail tại tầng service, không Add; message phản ánh quá ngắn (không dùng kiểu &quot;quá lớn&quot;).
        /// </summary>
        [Fact]
        public void UTCID09_CreateChapter_Fail_WhenContentShorterThan500Characters()
        {
            LogUtcContext("UTCID09",
                "FREE: content có ký tự nhưng độ dài < 500 → spec: fail.",
                "Precondition: story + author; order_index chưa trùng.",
                "Input: Content length = 499; AccessType=FREE; CoinPrice=0.",
                "Kỳ vọng: InvalidOperationException (quá ngắn / tối thiểu 500 ký tự) + không Add.");

            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = new string('a', 499),
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 0
            };

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(req, ownerAuthorId));
            Assert.Contains("quá ngắn", ex.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("500", ex.Message, StringComparison.Ordinal);
            Assert.DoesNotContain("quá lớn", ex.Message, StringComparison.OrdinalIgnoreCase);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        /// <summary>
        /// UTCID10 – coinPrice không phải số (ví dụ JSON gửi chuỗi "aa"): spec yêu cầu fail.
        /// CreateChapterRequestDto.CoinPrice là int? nên không thể mô phỏng "aa" trực tiếp trong C#;
        /// case spec tương ứng request body JSON với kiểu sai — validate xảy ra khi deserialize (cùng kiểu với [FromBody] API).
        /// Không assert đúng từng chữ message ("Giá trị chỉ nhận number").
        /// </summary>
        [Fact]
        public void UTCID10_CreateChapter_Fail_WhenCoinPriceIsNotNumeric()
        {
            LogUtcContext("UTCID10",
                "coinPrice trong JSON không phải số (chuỗi \"aa\") → fail khi deserialize.",
                "Layer: System.Text.Json + CreateChapterRequestDto (int? CoinPrice); không gọi ChapterService.",
                "Input: accessType FREE; orderIndex=1; content len=500; coinPrice JSON string \"aa\".",
                "Kỳ vọng: JsonException.");

            var id = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var content = new string('a', 500);
            var json =
                $$"""{"id":"{{id}}","storyId":"{{storyId}}","title":"Chương 1","content":"{{content}}","orderIndex":1,"accessType":"FREE","coinPrice":"aa"}""";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateChapterRequestDto>(json, options));
        }

        /// <summary>
        /// UTCID11 – order_index không phải số (ví dụ JSON gửi chuỗi "một"): spec yêu cầu fail.
        /// CreateChapterRequestDto.OrderIndex là int nên không thể gán chuỗi trực tiếp trong C#;
        /// case spec tương ứng body JSON sai kiểu — validate khi deserialize (giống [FromBody] API).
        /// Không assert đúng từng chữ message ("Giá trị chỉ nhận number").
        /// </summary>
        [Fact]
        public void UTCID11_CreateChapter_Fail_WhenOrderIndexIsNotNumeric()
        {
            LogUtcContext("UTCID11",
                "order_index trong JSON không phải số (chuỗi \"một\") → JsonException khi deserialize.",
                "Layer: deserialize request body; OrderIndex là int trong DTO.",
                "Input: content len=500; FREE; coinPrice=0.",
                "Kỳ vọng: JsonException.");

            var id = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var content = new string('b', 500);
            var json =
                $$"""{"id":"{{id}}","storyId":"{{storyId}}","title":"Chương 1","content":"{{content}}","orderIndex":"một","accessType":"FREE","coinPrice":0}""";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateChapterRequestDto>(json, options));
        }

        /// <summary>
        /// UTCID12 – coinPrice không phải số (chuỗi "aa"), biến thể matrix: context khác UTCID10 (ở đây accessType = PAID).
        /// Cùng bản chất với UTCID10: kiểu sai trong JSON → fail khi deserialize; không assert đúng từng chữ message.
        /// </summary>
        [Fact]
        public void UTCID12_CreateChapter_Fail_WhenCoinPriceIsNotNumeric_CombinatorialPaid()
        {
            LogUtcContext("UTCID12",
                "Cùng rule UTCID10 (coinPrice JSON \"aa\") nhưng matrix PAID — vẫn fail tại deserialize.",
                "Input: AccessType=PAID; orderIndex=1; content len=500; coinPrice string invalid.",
                "Kỳ vọng: JsonException (chưa tới rule PAID/views của service).");

            var id = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var content = new string('c', 500);
            var json =
                $$"""{"id":"{{id}}","storyId":"{{storyId}}","title":"Chương 1","content":"{{content}}","orderIndex":1,"accessType":"PAID","coinPrice":"aa"}""";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateChapterRequestDto>(json, options));
        }

        /// <summary>
        /// UTCID13 – user không phải chủ story: <see cref="ChapterService.Create"/> so <c>authorId</c> với <c>story.author_id</c>; API trả 403.
        /// </summary>
        [Fact]
        public void UTCID13_CreateChapter_Fail_WhenUserIsNotStoryAuthor()
        {
            LogUtcContext("UTCID13",
                "User không phải chủ story (ma trận: không phải tác giả truyện) → chỉ owner được tạo chapter.",
                "Precondition: story tồn tại; payload hợp lệ; order_index chưa trùng.",
                "Input: Create(req, loggedInUserNotOwner) với Guid khác story.author_id.",
                "Kỳ vọng: UnauthorizedAccessException + message chứa \"Bạn không phải tác giả\" + không Add.");

            var ownerAuthorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = ownerAuthorId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = new string('d', 500),
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 0
            };

            var loggedInUserNotOwner = Guid.NewGuid();
            var ex = Assert.Throws<UnauthorizedAccessException>(() => sut.Create(req, loggedInUserNotOwner));
            Assert.Equal("Bạn không phải tác giả của truyện này.", ex.Message);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        /// <summary>
        /// UTCID14 – tác giả (user khác) không phải chủ <c>story.author_id</c> không được tạo chương; message kiểu &quot;Bạn không phải tác giả của truyện này&quot;; API 403.
        /// </summary>
        [Fact]
        public void UTCID14_CreateChapter_Fail_WhenCallerAuthorDoesNotOwnStory()
        {
            LogUtcContext("UTCID14",
                "Tác giả khác chủ story (có thể là author trên hệ thống nhưng không sở hữu story này) → không được tạo chapter.",
                "Precondition: story.author_id = storyOwnerId; payload hợp lệ.",
                "Input: Create(req, anotherAuthorUserId) ≠ storyOwnerId.",
                "Kỳ vọng: UnauthorizedAccessException, message đầy đủ, không Add.");

            var storyOwnerId = Guid.NewGuid();
            var anotherAuthorUserId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                title = "Truyen A",
                author_id = storyOwnerId,
                story_progress_status = "ONGOING",
                total_views = 1000
            };

            var chapterStore = new List<chapters>();
            var sut = CreateSut(story, chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                out _,
                out _);

            var req = new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Chương 1",
                Content = new string('e', 500),
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 0
            };

            var ex = Assert.Throws<UnauthorizedAccessException>(() => sut.Create(req, anotherAuthorUserId));
            Assert.Equal("Bạn không phải tác giả của truyện này.", ex.Message);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        /// <summary>
        /// UTCID15 – order_index chỉ khoảng trắng (input rỗng về nghiệp vụ). Một số bảng testcase ghi mã UTCID16 cho cùng kịch bản.
        /// OrderIndex là int trong DTO; mô phỏng bằng JSON gửi chuỗi chỉ whitespace — fail khi deserialize (giống [FromBody] API).
        /// Không assert đúng từng chữ message ("Vui lòng điền đầy đủ thông tin").
        /// </summary>
        [Fact]
        public void UTCID15_CreateChapter_Fail_WhenOrderIndexIsWhitespaceString()
        {
            LogUtcContext("UTCID15",
                "order_index là chuỗi chỉ whitespace trong JSON → không parse được sang int.",
                "Một số tài liệu gọi mã UTCID16 cho cùng kịch bản.",
                "Layer: JsonException khi Deserialize; không gọi ChapterService.",
                "Input: orderIndex JSON \"   \" (spaces); content len=500; FREE.");

            var id = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var content = new string('f', 500);
            var json =
                $$"""{"id":"{{id}}","storyId":"{{storyId}}","title":"Chương 1","content":"{{content}}","orderIndex":"   ","accessType":"FREE","coinPrice":0}""";

            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            };

            Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CreateChapterRequestDto>(json, options));
        }
    }
}




//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT02_FunctionCreateChapter"