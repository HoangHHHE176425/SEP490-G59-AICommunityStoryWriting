using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using Repositories;
using Services.DTOs.Stories;
using Services.Implementations;
using Services.Interfaces;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateStory
    {
        public class TestLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _output;
            public TestLogger(ITestOutputHelper output) => _output = output;
            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _output.WriteLine(formatter(state, exception));
        }

        private readonly ITestOutputHelper _output;

        public UT_CreateStory(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

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
                _output.WriteLine($"Exception type: {ex.GetType().Name}");
                _output.WriteLine($"Message: {ex.Message}");
            }
            else
            {
                _output.WriteLine("OUTPUT : SUCCESS");
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        /// <summary>In-memory <c>List&lt;stories&gt;</c> do mock <c>IStoryRepository.Add</c> ghi vào — xem sau khi gọi <c>CreateSut</c> + <c>StoryService.Create</c> (cần <c>--logger \"console;verbosity=detailed\"</c> trên CLI).</summary>
        private void LogStoryStore(string label, IReadOnlyList<stories> store)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {label} — storyStore ({store.Count} phần tử) ========");
            if (store.Count == 0)
            {
                _output.WriteLine("  (rỗng)");
                return;
            }

            for (var i = 0; i < store.Count; i++)
            {
                var s = store[i];
                var titlePreview = s.title == null ? "" : s.title.Length <= 24 ? s.title : s.title[..24] + "…";
                _output.WriteLine(
                    $"  [{i}] id={s.id}, slug={s.slug}, status={s.status}, progress={s.story_progress_status}, author_id={s.author_id}, title=\"{titlePreview}\" (len={s.title?.Length ?? 0}), summaryLen={s.summary?.Length ?? 0}, cover={s.cover_image}");
            }
        }

        private StoryService CreateSut(
            List<stories> storyStore,
            out Mock<IStoryRepository> storyRepoMock,
            out Mock<IChapterRepository> chapterRepoMock,
            out Mock<IUserLookup> userLookupMock,
            out Mock<ICategoryLookup> categoryLookupMock)
        {
            storyRepoMock = new Mock<IStoryRepository>(MockBehavior.Strict);
            chapterRepoMock = new Mock<IChapterRepository>(MockBehavior.Strict);
            userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            categoryLookupMock = new Mock<ICategoryLookup>(MockBehavior.Strict);

            storyRepoMock.Setup(x => x.GetBySlug(It.IsAny<string>()))
                .Returns((string slug) => storyStore.FirstOrDefault(s => s.slug == slug));
            // In-memory data store
            storyRepoMock.Setup(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()))
                .Callback((stories s, IEnumerable<Guid> _) => storyStore.Add(s));

            userLookupMock.Setup(x => x.Exists(It.IsAny<Guid>())).Returns(true);
            userLookupMock.Setup(x => x.IsAuthor(It.IsAny<Guid>())).Returns(true);
            userLookupMock.Setup(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>())).Returns(false);

            var logger = new TestLogger<StoryService>(_output);
            var cache = new MemoryCache(new MemoryCacheOptions());
            return new StoryService(
                storyRepoMock.Object,
                chapterRepoMock.Object,
                userLookupMock.Object,
                categoryLookupMock.Object,
                logger,
                cache);
        }

        /// <summary>
        /// UTCID01 – happy path: user/author hợp lệ, không suspend, toàn bộ field hợp lệ → tạo truyện thành công.
        /// Gọi trực tiếp <see cref="StoryService.Create"/> (instance <see cref="StoryService"/> như tầng service thật).
        /// </summary>
        [Fact]
        public void UTCID01_Create_Success_WhenAllInputsValid()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            StoryService storyService = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var title = "Tiên Kiếm Ký: Hành Trình Mở Cõi";
            var summary = "Vào một buổi sớm mùa thu, Lục Vân rời ngôi làng nhỏ ven núi để tìm tung tích thanh kiếm cổ mà cha để lại trước khi mất tích. " +
                          "Trên đường đi, cậu kết bạn với một dược sư trẻ và một nữ hiệp luôn che giấu thân phận thật. " +
                          "Mỗi chặng đường đều mở ra những bí mật về các tông môn, lời nguyền cũ và cuộc chiến giữa chính đạo và ma đạo. " +
                          "Dù còn non trẻ, Lục Vân buộc phải học cách lựa chọn giữa thù hận cá nhân và trách nhiệm bảo vệ những người vô tội.";
            var coverImageUrl = "https://example.com/covers/valid-story-cover.jpg";

            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var dto = storyService.Create(request, authorId, coverImageUrl);
            LogTestCase(
                utcId: "UTCID01",
                spec: "Tạo truyện thành công với Author hợp lệ, không bị ban, slug chưa tồn tại.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverImageUrl
                },
                output: dto,
                ex: null);

            // Assert
            Assert.NotNull(dto);
            Assert.Equal(title, dto.Title);
            Assert.Equal(summary, dto.Summary);
            Assert.Equal(authorId, dto.AuthorId);
            Assert.Contains(categoryId, dto.CategoryIds!);
            Assert.Equal("ONGOING", dto.StoryProgressStatus);
            Assert.Equal("13+", dto.AgeRating);
            Assert.Equal(coverImageUrl, dto.CoverImage);
            Assert.Equal("DRAFT", dto.Status);
            Assert.NotEqual(Guid.Empty, dto.Id);
            Assert.Single(storyStore);

            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Once);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();

        }

        /// <summary>
        /// UTCID02 – cùng Title với truyện đã có (ví dụ Author A rồi Author B): spec cho phép trùng tên hiển thị, không coi là lỗi duplicate title.
        /// Product: slug = base từ Title + hậu tố số khi trùng DB → hai story cùng Title vẫn tạo được (UTCID02).
        /// Không assert đúng từng chữ log.
        /// </summary>
        [Fact]
        public void UTCID02_Create_Success_WhenTitleDuplicatesExistingStory()
        {
            // Arrange
            var existingAuthorId = Guid.NewGuid();
            var currentAuthorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var duplicatedTitle = "Tiên Kiếm Hiệp";
            var summary =
                "Giữa thời loạn lạc, một thiếu niên mang huyết mạch cổ xưa bước vào giang hồ với thanh kiếm gãy và lời hứa chưa kịp hoàn thành của cha mình. " +
                "Trên hành trình tìm lại chân tướng về vụ thảm sát năm xưa, cậu phải đối mặt với những môn phái tranh quyền, những kẻ săn lùng bí kíp và cả " +
                "những lựa chọn khiến lòng người đổi thay. Cùng các đồng đội mới quen, cậu dần khám phá bí mật về thanh kiếm, về thân thế thật sự của bản thân " +
                "và về trận chiến có thể làm thay đổi vận mệnh của cả võ lâm.";
            var coverUrl = "https://example.com/covers/cover-utc02.jpg";

            var storyStore = new List<stories>
            {
                new()
                {
                    id = Guid.NewGuid(),
                    title = duplicatedTitle,
                    slug = "tien-kiem-hiep",
                    summary = "Story đã tồn tại để tạo xung đột slug.",
                    author_id = existingAuthorId,
                    status = "DRAFT",
                    story_progress_status = "ONGOING",
                    age_rating = "13+",
                    created_at = DateTime.Now,
                    updated_at = DateTime.Now
                }
            };
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);
            var request = new CreateStoryRequestDto
            {
                Title = duplicatedTitle,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var dto = sut.Create(request, currentAuthorId, coverUrl);
            LogTestCase(
                utcId: "UTCID02",
                spec: "Title trùng, slug gốc đã tồn tại thì hệ thống tự generate slug unique và vẫn tạo thành công.",
                input: new
                {
                    ExistingSlug = "tien-kiem-hiep",
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = currentAuthorId,
                    CoverImageUrl = coverUrl
                },
                output: dto,
                ex: null);

            // Assert
            Assert.NotNull(dto);
            Assert.Equal(duplicatedTitle, dto.Title);
            Assert.Equal(currentAuthorId, dto.AuthorId);
            Assert.Equal(summary, dto.Summary);
            Assert.StartsWith("tien-kiem-hiep", dto.Slug);
            Assert.NotEqual("tien-kiem-hiep", dto.Slug);
            Assert.True(dto.Slug == "tien-kiem-hiep-1" || dto.Slug == "tien-kiem-hiep-2" || dto.Slug.StartsWith("tien-kiem-hiep-"));
            Assert.Equal(2, storyStore.Count);

            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Once);
            userLookupMock.Verify(x => x.Exists(currentAuthorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(currentAuthorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID03 – thiếu tác giả (null / không xác định author): spec yêu cầu fail, không tạo story.
        /// API service nhận authorId kiểu Guid — mô phỏng “author null” bằng Guid.Empty; IUserLookup.Exists(Empty) = false.
        /// Không assert đúng từng chữ message ("Vui lòng điền đầy đủ thông tin").
        /// Bảng spec ghi Title &lt; 50 ký tự vẫn hợp lệ; Create hiện không validate độ dài title — dùng title ngắn cho đúng matrix.
        /// </summary>
        [Fact]
        public void UTCID03_Create_Fail_WhenAuthorNotFoundInSystem()
        {
            // Arrange
            var missingAuthorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            userLookupMock.Reset();
            userLookupMock.Setup(x => x.Exists(missingAuthorId)).Returns(false);

            var title = "Tiên Kiếm Phong Vân";
            var summary =
                "Sau biến cố tại biên trấn, một kiếm khách trẻ tuổi lên đường tìm lại tung tích người thầy đã biến mất giữa đêm mưa. " +
                "Trên hành trình qua các thành trấn và sơn môn, cậu liên tục vướng vào những ân oán giang hồ tưởng chừng không liên quan nhưng lại " +
                "dần hé lộ một âm mưu lớn hơn nhắm vào cả võ lâm. Dù nhiều lần đứng trước lựa chọn khó khăn, cậu vẫn quyết giữ lời thề bảo vệ người yếu thế " +
                "và truy ra sự thật phía sau thanh cổ kiếm đang thức tỉnh từng ngày trong tay mình.";
            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/x.jpg";
            var ex = Record.Exception(() => sut.Create(request, missingAuthorId, coverUrl));
            LogTestCase(
                utcId: "UTCID03",
                spec: "Không tìm thấy Author trong hệ thống thì fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = missingAuthorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(missingAuthorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>()), Times.Never);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID04 – StoryProgressStatus không thuộc danh sách hợp lệ (ONGOING/COMPLETED/HIATUS) → fail, không tạo story.
        /// </summary>
        [Fact]
        public void UTCID04_Create_Fail_WhenStoryProgressStatusInvalid()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var title = "Tiên Kiếm Minh Không";
            var summary =
                "Trong một đêm trăng mờ, cổ kiếm trong tủ gỗ đột nhiên rung nhẹ như gọi chủ nhân đến một nhiệm vụ chưa hoàn thành. " +
                "Thiếu niên mang họ Lục quyết lên đường tìm lại bí kíp thất truyền và lời giải cho cái chết bí ẩn của sư phụ. " +
                "Trên đường đi, cậu gặp những kẻ thù giả làm bạn, những minh chứng bị che giấu và cả những lựa chọn khiến lương tri rung chuyển. " +
                "Mỗi bước chân đều đưa cậu gần hơn tới chân tướng, nhưng cũng khiến cậu phải trả giá bằng niềm tin và máu của chính mình.";
            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ABC"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc04-tien-kiem.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID04",
                spec: "StoryProgressStatus không hợp lệ (ví dụ ABC) → ArgumentException, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID05 – StoryProgressStatus null (DTO cho phép null): service bắt buộc → fail.
        /// Ma trận có thể ghi “Vui lòng điền đầy đủ thông tin” — không assert khớp từng chữ.
        /// </summary>
        [Fact]
        public void UTCID05_Create_Fail_WhenStoryProgressStatusNull()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var title = "Tiên Kiếm Minh Không";
            var summary =
                "Trong một đêm trăng mờ, cổ kiếm trong tủ gỗ đột nhiên rung nhẹ như gọi chủ nhân đến một nhiệm vụ chưa hoàn thành. " +
                "Thiếu niên mang họ Lục quyết lên đường tìm lại bí kíp thất truyền và lời giải cho cái chết bí ẩn của sư phụ. " +
                "Trên đường đi, cậu gặp những kẻ thù giả làm bạn, những minh chứng bị che giấu và cả những lựa chọn khiến lương tri rung chuyển. " +
                "Mỗi bước chân đều đưa cậu gần hơn tới chân tướng, nhưng cũng khiến cậu phải trả giá bằng niềm tin và máu của chính mình.";
            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = null
            };

            // Act
            var coverUrl = "https://example.com/covers/utc05-tien-kiem.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID05",
                spec: "StoryProgressStatus = null → InvalidOperationException, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID06 – CategoryId không tồn tại trong hệ thống → spec: fail, không tạo story.
        /// Input giống payload thật (tiêu đề/mô tả tiếng Việt, 13+, ONGOING, URL ảnh); chỉ <c>CategoryIds</c> trỏ Guid chưa có trong lookup.
        /// Product: GetById null → InvalidOperationException. Không assert đúng từng chữ message.
        /// </summary>
        [Fact]
        public void UTCID06_Create_Fail_WhenCategoryIdDoesNotExist()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var missingCategoryId = Guid.NewGuid();

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(missingCategoryId)).Returns((categories?)null);

            var title = "Hỏa Long Kiếm Ấn";
            var summary =
                "Giang hồ đồn thổi về một ấn kiếm chôn dưới nền miếu hoang, ai chạm vào là mang họa cho cả môn phái. " +
                "Nữ hiệp trẻ tuổi từ giang nam ra bắc, chỉ vì một lá thư mực nhòe, phải đối mặt với minh chứng giả và lòng người khó đoán. " +
                "Cô không cần danh tiếng, chỉ cần sự thật: ai đã giết cha cô, và vì sao ấn kiếm lại mang họ của nhà họ Diệp. " +
                "Trên con đường đó, tình nghĩa sư muội, lời thề bang hội và những đêm mưa gió đều thử thách xem cô còn giữ được lưỡi kiếm thẳng hay không.";
            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = new List<Guid> { missingCategoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc06-hoa-long.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID06",
                spec: "CategoryId không tồn tại → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(storyStore);
            Assert.IsType<InvalidOperationException>(ex);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(missingCategoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID07 – CategoryIds null (thiếu thể loại): spec yêu cầu fail, không tạo story.
        /// Input dùng dữ liệu thật (tiêu đề/mô tả tiếng Việt, 13+, ONGOING, ảnh hợp lệ), chỉ thiếu CategoryIds.
        /// Product: <c>CategoryIds == null || !Any()</c> → InvalidOperationException ("Chọn ít nhất một thể loại.").
        /// Không assert đúng từng chữ message.
        /// </summary>
        [Fact]
        public void UTCID07_Create_Fail_WhenCategoryIdsNull()
        {
            var authorId = Guid.NewGuid();
            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            var title = "Bạch Nguyệt Sơn Hà";
            var summary =
                "Từ ngôi làng ven sông bị thiêu rụi trong một đêm không trăng, thiếu niên họ Trần bước vào thế giới tu chân với lời thề phải tìm ra kẻ chủ mưu. " +
                "Mỗi cánh cửa tông môn mở ra lại kéo theo một bí mật cũ, nơi bằng hữu và phản đồ chỉ cách nhau một lời hứa. " +
                "Giữa những trận pháp cổ và thư tịch thất truyền, cậu nhận ra vận mệnh của mình gắn chặt với thanh kiếm trắng trong truyền thuyết. " +
                "Hành trình báo thù dần biến thành cuộc chiến bảo vệ những người còn sống, ngay cả khi phải đánh đổi danh tiếng và tuổi trẻ.";
            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = null!,
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc07-bach-nguyet.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID07",
                spec: "CategoryIds = null → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(storyStore);
            Assert.IsType<InvalidOperationException>(ex);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID08 – Summary null: thiếu dữ liệu bắt buộc, phải fail và không tạo story.
        /// </summary>
        [Fact]
        public void UTCID08_Create_Fail_WhenSummaryNull()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var title = "Phong Vân Kiếm Lục";
            var coverUrl = "https://example.com/covers/utc08-phong-van.jpg";
            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = null,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID08",
                spec: "Summary = null → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID09 – Title vượt giới hạn nghiệp vụ (max 50 ký tự khi tạo): fail, không Add.
        /// </summary>
        [Fact]
        public void UTCID09_Create_Fail_WhenTitleExceeds50Characters()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var titleOverLimit = "Huyền Thiên Kiếm Khúc Và Bí Ẩn Long Mạch Đế Vương Triều";
            var summary =
                "Giữa thời loạn lạc, một bản kiếm phổ cổ xuất hiện khiến các môn phái tranh đoạt bằng mọi giá. " +
                "Người giữ bản đồ long mạch lại là một thư sinh từng thề rời xa giang hồ sau biến cố gia tộc. " +
                "Khi từng manh mối dẫn về triều đình mục nát, chàng buộc phải chọn giữa báo thù và cứu dân khỏi chiến hỏa. " +
                "Mỗi bước đi đều có thể đổi lấy mạng sống của bằng hữu, nhưng cũng mở ra cơ hội chấm dứt vòng lặp máu và quyền lực.";
            var request = new CreateStoryRequestDto
            {
                Title = titleOverLimit,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc09.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID09",
                spec: "Title vượt giới hạn → fail, không Add.",
                input: new
                {
                    TitleLength = request.Title?.Length ?? 0,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID10 – ảnh bìa sai định dạng: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Lỗi định dạng" — không assert đúng từng chữ.
        /// </summary>
        [Fact]
        public void UTCID10_Create_Fail_WhenCoverImageFormatInvalid()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var request = new CreateStoryRequestDto
            {
                Title = "Vân Hải Dị Kiếm",
                Summary =
                    "Đại lục chia cắt bởi biển mây và chiến tranh kéo dài nhiều thế hệ, nơi mỗi tộc người đều giữ một mảnh bí thuật cổ xưa. " +
                    "Khi thanh dị kiếm thất lạc tái xuất, một tiểu đội lữ khách bất đắc dĩ phải hợp tác để truy tìm chân tướng trước khi các thế lực lớn ra tay. " +
                    "Từ thành trì sương trắng đến vực lửa phương nam, họ dần nhận ra kẻ thù thật sự không chỉ là triều đình mà còn là nỗi sợ bên trong mỗi người. " +
                    "Cuộc hành trình buộc họ chọn giữa quyền lực tuyệt đối và cái giá của lòng nhân còn sót lại.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc10-invalid.bmp";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID10",
                spec: "CoverImageUrl sai định dạng ảnh hợp lệ (jpg/jpeg/png/gif/webp) → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID11 – ảnh bìa vượt giới hạn dung lượng (MB): fail, không tạo story.
        /// </summary>
        [Fact]
        public void UTCID11_Create_Fail_WhenCoverImageTooLarge()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var request = new CreateStoryRequestDto
            {
                Title = "Thiên Mệnh Kiếm Đồ",
                Summary =
                    "Một bản đồ cổ ghi lại đường đi đến kiếm trận tối thượng vô tình rơi vào tay thiếu chủ của môn phái đã suy tàn. " +
                    "Để bảo vệ người thân, anh buộc phải liên minh với những kẻ từng là cừu địch và bước vào cuộc truy sát khắp ba châu. " +
                    "Mỗi tọa độ trên bản đồ đều đổi bằng máu, nhưng càng tiến gần đích đến, anh càng nhận ra bí mật lớn nhất lại liên quan trực tiếp đến thân thế của mình. " +
                    "Giữa tham vọng thống nhất võ lâm và lời hứa với sư phụ, anh chỉ có một con đường để chọn.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc11-large.jpg?sizeMb=6";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID11",
                spec: "Cover image size > 5MB (sizeMb=6) → fail, không Add.",
                input: new
                {
                    request.Title,
                    CoverSizeMb = 6,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID12 – dữ liệu vượt quá giới hạn: Summary quá dài so với ngưỡng cho phép của service.
        /// </summary>
        [Fact]
        public void UTCID12_Create_Fail_WhenSummaryExceedsMaxLength()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);
            var summaryOverLimit = string.Concat(Enumerable.Repeat(
                "Hành trình qua cửu vực mở ra từng bí mật cổ xưa, nơi mỗi quyết định đều đổi bằng danh dự, máu và lời thề của cả một môn phái. ",
                40));

            var request = new CreateStoryRequestDto
            {
                Title = "Huyết Ảnh Kiếm Tông",
                Summary = summaryOverLimit,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc12.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID12",
                spec: "Summary vượt quá giới hạn cho phép → fail, không Add.",
                input: new
                {
                    request.Title,
                    SummaryLength = request.Summary?.Length ?? 0,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID13 – Title null: thiếu dữ liệu bắt buộc, fail và không tạo story.
        /// </summary>
        [Fact]
        public void UTCID13_Create_Fail_WhenTitleNull()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var request = new CreateStoryRequestDto
            {
                Title = null!,
                Summary =
                    "Dưới chân thành cổ, một lời nguyền bị phong ấn suốt trăm năm bất ngờ thức tỉnh, kéo theo hàng loạt cái chết bí ẩn của những người canh mộ. " +
                    "Nữ hiệp trẻ phải bước vào mê cung ngầm để tìm chân tướng và cứu lấy sư môn trước khi triều đình đổ toàn bộ tội lỗi lên đầu họ. " +
                    "Mỗi dấu tích khắc trên vách đá đều dẫn về một bí mật bị xóa khỏi chính sử, nơi công lý bị đổi lấy quyền lực. " +
                    "Nếu thất bại, không chỉ một tông môn sụp đổ mà cả biên cương sẽ chìm trong loạn chiến.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc13.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID13",
                spec: "Title = null → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID14 – tài khoản đã đăng nhập nhưng không phải Author: phải fail và không tạo story.
        /// </summary>
        [Fact]
        public void UTCID14_Create_Fail_WhenLoggedInUserIsNotAuthor()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);
            userLookupMock.Setup(x => x.Exists(authorId)).Returns(true);
            userLookupMock.Setup(x => x.IsAuthor(authorId)).Returns(false);

            var request = new CreateStoryRequestDto
            {
                Title = "Lam Nguyệt Kiếm Ca",
                Summary =
                    "Sau biến cố tại biên trấn, một thiếu hiệp mang thân phận thường dân bị cuốn vào vòng tranh đoạt bí pháp giữa các tông môn lớn. " +
                    "Dù có thiên phú kiếm đạo hiếm gặp, cậu vẫn không thể bước vào đường tu nếu thiếu danh phận được công nhận bởi giới giang hồ. " +
                    "Khi âm mưu của triều đình lộ diện, cậu buộc phải hợp tác với những người từng coi thường mình để bảo vệ dân chúng khỏi một cuộc thảm sát. " +
                    "Con đường trở thành kiếm khách thực thụ bắt đầu từ việc đối diện sự thật rằng tài năng không thay thế được tư cách hợp lệ.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc14.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID14",
                spec: "User đã đăng nhập nhưng role != Author → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthor(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>()), Times.Never);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID15 – Title input chỉ chứa khoảng trắng: fail, không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// </summary>
        [Fact]
        public void UTCID15_Create_Fail_WhenTitleIsWhitespace()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var request = new CreateStoryRequestDto
            {
                Title = "   \t  \r\n  ",
                Summary =
                    "Một thanh niên vô danh tìm được mảnh ngọc cổ trong đêm mưa, từ đó bị kéo vào cuộc truy đuổi của nhiều thế lực giang hồ. " +
                    "Mỗi dấu vết để lại đều dẫn đến một bí mật của triều đại đã mất, nơi danh vọng và phản bội luôn đi cùng nhau. " +
                    "Khi chân tướng dần lộ ra, cậu phải quyết định bảo vệ người thân hay giữ lời thề với sư môn trước cơn bão sắp đến. " +
                    "Con đường kiếm đạo bắt đầu bằng việc học cách đối diện chính mình giữa những lựa chọn không có đáp án trọn vẹn.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc15.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID15",
                spec: "Title chỉ chứa khoảng trắng → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthor(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID16 – author bị ban (không còn đủ điều kiện Author): fail, không tạo story.
        /// Ma trận có thể ghi "Author đã bị BAN" — không assert đúng từng chữ.
        /// </summary>
        [Fact]
        public void UTCID16_Create_Fail_WhenAuthorIsBanned()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            userLookupMock.Setup(x => x.Exists(authorId)).Returns(true);
            userLookupMock.Setup(x => x.IsAuthor(authorId)).Returns(false);

            var request = new CreateStoryRequestDto
            {
                Title = "Tàn Kiếm Mộ Vân",
                Summary =
                    "Trong đêm tuyết phủ, cựu kiếm sĩ từng một thời lừng danh bị vu oan phản quốc và truy nã khắp thiên hạ. " +
                    "Khi trở về cố hương để minh oan, anh phát hiện cả tông môn đã bị biến thành quân cờ trong một kế hoạch thao túng triều chính kéo dài nhiều năm. " +
                    "Mỗi bằng chứng thu được đều kéo theo một sự hi sinh, buộc anh đối diện với những người bạn cũ nay đứng ở chiến tuyến đối nghịch. " +
                    "Con đường lấy lại danh dự cũng là hành trình chuộc lại lỗi lầm của chính mình trước những người đã ngã xuống.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING",
                CoverImage = null
            };

            // Act
            var coverUrl = "https://example.com/covers/utc16-tan-kiem.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverImageUrl: coverUrl));
            LogTestCase(
                utcId: "UTCID16",
                spec: "Author bị ban (IsAuthor = false) → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthor(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>()), Times.Never);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID17 – Image bị null: thiếu dữ liệu bắt buộc, phải fail và không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// </summary>
        [Fact]
        public void UTCID17_Create_Fail_WhenImageIsNull()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var request = new CreateStoryRequestDto
            {
                Title = "Huyết Vân Lục",
                Summary =
                    "Giữa thời cuộc phân tranh, một đội hiệp khách vô tình giải phong ấn cổ trận dưới lòng đất, kéo theo những điềm báo đẫm máu trên khắp cửu châu. " +
                    "Người thủ lĩnh trẻ tuổi buộc phải chọn giữa bảo toàn môn phái và ngăn chặn đại họa có thể xóa sổ hàng ngàn sinh mạng vô tội. " +
                    "Càng lần theo dấu tích của cổ trận, họ càng phát hiện những liên minh tưởng như chính nghĩa lại che giấu tham vọng thâu tóm thiên hạ. " +
                    "Mỗi quyết định đều đổi bằng sinh mạng đồng đội, nhưng rút lui lúc này đồng nghĩa để tai ương nuốt trọn thế gian.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var ex = Record.Exception(() => sut.Create(request, authorId, coverImageUrl: null));
            LogTestCase(
                utcId: "UTCID17",
                spec: "CoverImageUrl = null → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = (string?)null
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthor(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID18 – AgeRating không hợp lệ: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Độ tuổi không hợp lệ" — không assert đúng từng chữ.
        /// </summary>
        [Fact]
        public void UTCID18_Create_Fail_WhenAgeRatingInvalid()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var request = new CreateStoryRequestDto
            {
                Title = "Trường Sinh Lạc Ấn",
                Summary =
                    "Một phù ấn trường sinh xuất hiện giữa chợ đen khiến các gia tộc lớn cùng truy tìm người sở hữu thật sự. " +
                    "Thiếu nữ giữ bí mật ấy phải bôn tẩu qua nhiều thành trì, vừa trốn sát thủ vừa học cách kiểm soát sức mạnh có thể phá vỡ cân bằng tam giới. " +
                    "Càng đi sâu vào âm mưu, cô càng nhận ra người đứng sau mọi biến cố lại chính là ân nhân từng cứu mạng mình thuở nhỏ. " +
                    "Lựa chọn cuối cùng sẽ quyết định vận mệnh của cả tông môn lẫn những người đã đặt niềm tin vào cô.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "15+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc18.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID18",
                spec: "AgeRating không thuộc whitelist (ALL/13+/16+/18+) → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthor(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID19 – input age null: thiếu/không hợp lệ AgeRating, phải fail và không tạo story.
        /// Ma trận có thể ghi "Độ tuổi không hợp lệ" — không assert đúng từng chữ.
        /// </summary>
        [Fact]
        public void UTCID19_Create_Fail_WhenAgeRatingNull()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var title = "Thiên Địa Huyền Ca";
            var summary =
                "Tại vùng biên ải quanh năm sương phủ, một thiếu niên vô danh vô tình mở được cổ thư bị phong ấn từ thời thượng cổ. " +
                "Từ đó, cậu bị đẩy vào cuộc săn đuổi của nhiều thế lực muốn chiếm đoạt bí thuật khống chế thiên tượng để thay đổi cục diện chiến tranh. " +
                "Trên hành trình tìm minh sư và đồng đội, cậu dần hiểu rằng quyền năng càng lớn thì trách nhiệm càng nặng, và sai lầm nhỏ có thể đổi bằng mạng sống của cả một thành trì. " +
                "Để bảo vệ người thân, cậu buộc phải trưởng thành giữa những trận chiến không có chỗ cho lòng do dự.";
            var coverImageUrl = "https://example.com/covers/utc19-age-null.jpg";

            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = null!,
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var ex = Record.Exception(() => sut.Create(request, authorId, coverImageUrl));
            LogTestCase(
                utcId: "UTCID19",
                spec: "AgeRating = null → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverImageUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<ArgumentException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthor(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();

        }

        /// <summary>
        /// UTCID20 – matrix bổ sung: user đã đăng nhập role Author, slug chưa tồn tại, các field hợp lệ,
        /// nhưng chọn trạng thái "Tạm dừng (HIATUS)" khi truyện chưa xuất bản thì phải fail.
        /// Log kỳ vọng nghiệp vụ: "Truyện chưa được xuất bản không thể chọn trạng thái là tạm ngưng".
        /// </summary>
        [Fact]
        public void UTCID20_Create_Fail_WhenAuthorChoosesHiatusForNewStory()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var request = new CreateStoryRequestDto
            {
                Title = "Hàn Kiếm Tầm Long",
                Summary =
                    "Sau khi triều đình ban lệnh truy sát các môn phái cũ, một nhóm kiếm khách lưu vong phát hiện dấu vết long mạch có thể xoay chuyển cục diện thiên hạ. " +
                    "Người thủ lĩnh trẻ tuổi buộc phải dẫn đồng đội vượt qua vùng băng nguyên và thành cổ đổ nát để tìm bí mật trước khi kẻ thù nắm được. " +
                    "Mỗi bước đi đều đối mặt phản bội, bởi kẻ đứng sau màn sương lại là người từng kết nghĩa sinh tử với anh năm xưa. " +
                    "Nếu thất bại, không chỉ môn phái tan rã mà biên cương cũng chìm vào hỗn chiến kéo dài nhiều thế hệ.",
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "HIATUS"
            };
            var coverImageUrl = "https://example.com/covers/utc20-hiatus.jpg";

            // Act
            var ex = Record.Exception(() => sut.Create(request, authorId, coverImageUrl));
            LogTestCase(
                utcId: "UTCID20",
                spec: "Truyện mới chưa xuất bản nhưng chọn trạng thái HIATUS -> fail.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = authorId,
                    CoverImageUrl = coverImageUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthor(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }
    }

}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateStory." --logger "console;verbosity=detailed"