using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Repositories;
using Services.DTOs.Stories;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateStory
    {
        private readonly ITestOutputHelper _output;

        public UT_CreateStory(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
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
                _output.WriteLine($"TYPE   : {ex.GetType().Name}");
                _output.WriteLine($"MSG    : {ex.Message}");
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

        private static StoryService CreateSut(
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
            storyRepoMock.Setup(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()))
                .Callback((stories s, IEnumerable<Guid> _) => storyStore.Add(s));

            userLookupMock.Setup(x => x.Exists(It.IsAny<Guid>())).Returns(true);
            userLookupMock.Setup(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>())).Returns(false);

            var logger = NullLogger<StoryService>.Instance;
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

            var title = new string('a', 50);
            var summary = new string('b', 50);
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
                spec: "Tạo story thành công với input hợp lệ.",
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

            LogStoryStore("UTCID01 (sau verify)", storyStore);
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
            var authorA = Guid.NewGuid();
            var authorB = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var category = new categories
            {
                id = categoryId,
                name = "Tiểu thuyết",
                slug = "tieu-thuyet",
                is_active = true
            };

            var sharedTitle = new string('h', 50);
            var summaryA = new string('a', 50);
            var summaryB = new string('b', 50);
            var coverUrl = "https://example.com/covers/cover-utc02.jpg";

            var storyStore = new List<stories>();
            var sut = CreateSut(storyStore,
                out var storyRepoMock,
                out var chapterRepoMock,
                out var userLookupMock,
                out var categoryLookupMock);

            categoryLookupMock.Setup(x => x.GetById(categoryId)).Returns(category);

            var requestA = new CreateStoryRequestDto
            {
                Title = sharedTitle,
                Summary = summaryA,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };
            var requestB = new CreateStoryRequestDto
            {
                Title = sharedTitle,
                Summary = summaryB,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var dtoA = sut.Create(requestA, authorA, coverUrl);
            var dtoB = sut.Create(requestB, authorB, coverUrl);
            LogTestCase(
                utcId: "UTCID02",
                spec: "Trùng Title vẫn tạo được (Title không unique).",
                input: new
                {
                    RequestA = new
                    {
                        requestA.Title,
                        requestA.Summary,
                        requestA.CategoryIds,
                        requestA.AgeRating,
                        requestA.StoryProgressStatus,
                        AuthorId = authorA
                    },
                    RequestB = new
                    {
                        requestB.Title,
                        requestB.Summary,
                        requestB.CategoryIds,
                        requestB.AgeRating,
                        requestB.StoryProgressStatus,
                        AuthorId = authorB
                    },
                    CoverImageUrl = coverUrl
                },
                output: new { StoryA = dtoA, StoryB = dtoB },
                ex: null);

            // Assert
            Assert.NotNull(dtoA);
            Assert.NotNull(dtoB);
            Assert.Equal(sharedTitle, dtoA.Title);
            Assert.Equal(sharedTitle, dtoB.Title);
            Assert.NotEqual(dtoA.Id, dtoB.Id);
            Assert.Equal(authorA, dtoA.AuthorId);
            Assert.Equal(authorB, dtoB.AuthorId);
            Assert.Equal(summaryA, dtoA.Summary);
            Assert.Equal(summaryB, dtoB.Summary);
            Assert.Equal(2, storyStore.Count);

            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Exactly(2));
            userLookupMock.Verify(x => x.Exists(authorA), Times.Once);
            userLookupMock.Verify(x => x.Exists(authorB), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorA), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorB), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Exactly(2));
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID03 – thiếu tác giả (null / không xác định author): spec yêu cầu fail, không tạo story.
        /// API service nhận authorId kiểu Guid — mô phỏng “author null” bằng Guid.Empty; IUserLookup.Exists(Empty) = false.
        /// Không assert đúng từng chữ message ("Vui lòng điền đầy đủ thông tin").
        /// Bảng spec ghi Title &lt; 50 ký tự vẫn hợp lệ; Create hiện không validate độ dài title — dùng title ngắn cho đúng matrix.
        /// </summary>
        [Fact]
        public void UTCID03_Create_Fail_WhenAuthorIdMissingOrEmpty()
        {
            // Arrange
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
            userLookupMock.Setup(x => x.Exists(Guid.Empty)).Returns(false);

            var title = new string('c', 12);
            var summary = new string('d', 52);
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
            var ex = Record.Exception(() => sut.Create(request, Guid.Empty, coverUrl));
            LogTestCase(
                utcId: "UTCID03",
                spec: "Author không tồn tại (AuthorId = Guid.Empty) → fail, không Add.",
                input: new
                {
                    request.Title,
                    request.Summary,
                    request.CategoryIds,
                    request.AgeRating,
                    request.StoryProgressStatus,
                    AuthorId = Guid.Empty,
                    CoverImageUrl = coverUrl
                },
                output: null,
                ex: ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(Guid.Empty), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>()), Times.Never);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID04 – StoryProgressStatus (tiến độ truyện: Đang ra / Hoàn thành / Tạm dừng) không hợp lệ → spec: fail, không tạo story.
        /// Product: ONGOING, COMPLETED, HIATUS — giá trị khác → ArgumentException. Không assert đúng từng chữ ("Trạng thái không tồn tại").
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

            var request = new CreateStoryRequestDto
            {
                Title = new string('e', 20),
                Summary = new string('f', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "NOT_A_VALID_PROGRESS"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc04.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID04",
                spec: "StoryProgressStatus không hợp lệ → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(categoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID05 – StoryProgressStatus null/whitespace: bắt buộc; <see cref="StoryService.Create"/> ném <c>InvalidOperationException</c> (vd. đầy đủ thông tin).
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

            var request = new CreateStoryRequestDto
            {
                Title = new string('g', 18),
                Summary = new string('h', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = null
            };

            // Act
            var coverUrl = "https://example.com/covers/utc05.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID05",
                spec: "StoryProgressStatus = null → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID06 – CategoryId không tồn tại trong hệ thống → spec: fail, không tạo story.
        /// Product: GetById null → InvalidOperationException. Không assert đúng từng chữ ("Thể loại không tồn tại").
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

            var request = new CreateStoryRequestDto
            {
                Title = new string('k', 22),
                Summary = new string('m', 52),
                CategoryIds = new List<Guid> { missingCategoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc06.jpg";
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(missingCategoryId), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID07 – CategoryIds null (thiếu thể loại): spec yêu cầu fail, không tạo story.
        /// Product: <c>CategoryIds == null || !Any()</c> → InvalidOperationException ("Chọn ít nhất một thể loại.").
        /// Không assert đúng từng chữ ("Vui lòng điền đầy đủ thông tin").
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

            var request = new CreateStoryRequestDto
            {
                Title = new string('p', 20),
                Summary = new string('q', 52),
                CategoryIds = null!,
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc07.jpg";
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID08 – không còn dùng/bắt buộc Summary: <c>Summary = null</c> vẫn tạo truyện thành công.
        /// Create không validate Summary; lưu <c>null</c> vào entity và trả DTO.
        /// </summary>
        [Fact]
        public void UTCID08_Create_Success_WhenSummaryNull()
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

            var title = new string('r', 24);
            var coverUrl = "https://example.com/covers/utc08.jpg";
            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = null,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var dto = sut.Create(request, authorId, coverUrl);
            LogTestCase(
                utcId: "UTCID08",
                spec: "Summary = null vẫn tạo story thành công.",
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
                output: dto,
                ex: null);

            // Assert
            Assert.NotNull(dto);
            Assert.Equal(title, dto.Title);
            Assert.Null(dto.Summary);
            Assert.Equal(coverUrl, dto.CoverImage);
            Assert.Single(storyStore);
            Assert.Null(storyStore[0].summary);

            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Once);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID09 – Title vượt giới hạn nghiệp vụ (max 50 ký tự khi tạo): fail, không Add.
        /// </summary>
        [Fact]
        public void UTCID09_Create_Fail_WhenInputExceedsMaxLength()
        {
            // Arrange
            const int titleOverLimitChars = 20_000;
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
                Title = new string('w', titleOverLimitChars),
                Summary = new string('v', 52),
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
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID10 – sai định dạng (ví dụ AgeRating không đúng chuẩn hệ thống): spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi log "Lỗi định dạng" — không assert đúng từng chữ.
        /// Product hiện tại: Create whitelist AgeRating (ALL / 13+ / 16+ / 18+) → giá trị như "PG13" ném <see cref="ArgumentException"/>.
        /// </summary>
        [Fact]
        public void UTCID10_Create_Fail_WhenAgeRatingInvalidFormat()
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
                Title = new string('a', 24),
                Summary = new string('b', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "PG13",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc10.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID10",
                spec: "AgeRating sai format (không thuộc whitelist) → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID11 – Title vượt giới hạn nghiệp vụ (ma trận: &gt; 50 ký tự).
        /// </summary>
        [Fact]
        public void UTCID11_Create_Fail_WhenTitleExceedsMaxLength()
        {
            // Arrange
            const int specMaxTitleLength = 50;
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
                Title = new string('x', specMaxTitleLength + 1),
                Summary = new string('y', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc11.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID11",
                spec: "Title > 50 ký tự → fail, không Add.",
                input: new
                {
                    request.Title,
                    TitleLength = request.Title?.Length ?? 0,
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID12 – Title null: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product: không có validate required Title rõ ràng; <see cref="StoryService.Create"/> gọi <c>GenerateSlug(request.Title)</c> →
        /// <c>Title == null</c> thường ném <see cref="NullReferenceException"/> trước khi Add (fail + không lưu, nhưng không phải thông báo nghiệp vụ có chủ đích).
        /// </summary>
        [Fact]
        public void UTCID12_Create_Fail_WhenTitleNull()
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
                Summary = new string('z', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc12.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID12",
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
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID13 – người thực hiện không phải tác giả hợp lệ trong hệ thống: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Bạn không phải là tác giả" — không assert đúng từng chữ.
        /// Product: <see cref="StoryService.Create"/> gọi <see cref="IUserLookup.Exists"/> trước; user không tồn tại → <see cref="InvalidOperationException"/> (message hiện tại nói AuthorId không có trong bảng users).
        /// Khác UTCID03: dùng Guid không rỗng nhưng <c>Exists == false</c> (mô phỏng định danh không gắn user/tác giả thật trong DB).
        /// </summary>
        [Fact]
        public void UTCID13_Create_Fail_WhenCallerNotRegisteredAuthor()
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

            userLookupMock.Reset();
            userLookupMock.Setup(x => x.Exists(authorId)).Returns(false);

            var request = new CreateStoryRequestDto
            {
                Title = new string('e', 20),
                Summary = new string('f', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc13.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID13",
                spec: "AuthorId không tồn tại trong hệ thống (IUserLookup.Exists = false) → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>()), Times.Never);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID14 – Title chỉ gồm khoảng trắng: spec yêu cầu fail (tương đương <c>string.IsNullOrWhiteSpace</c>), không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product hiện tại: Create không gọi <c>IsNullOrWhiteSpace(Title)</c>; slug rỗng vẫn có thể Add → test FAIL cho đến khi product validate.
        /// </summary>
        [Fact]
        public void UTCID14_Create_Fail_WhenTitleWhitespaceOnly()
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
                Summary = new string('g', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc14.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID14",
                spec: "Title chỉ whitespace → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID15 – tác giả bị chặn tạo nội dung (spec: BAN): fail, không tạo story.
        /// Ma trận có thể ghi "Author đã bị BAN" — không assert đúng từng chữ.
        /// Product: <see cref="StoryService.Create"/> gọi <see cref="IUserLookup.IsAuthorWritingSuspended"/>; khi true → <see cref="InvalidOperationException"/>
        /// (message hiện tại: tạm khóa chức năng viết truyện). Không có kiểm tra <c>users.status == BANNED</c> trong Create — mô phỏng "bị chặn" bằng suspend viết.
        /// </summary>
        [Fact]
        public void UTCID15_Create_Fail_WhenAuthorWritingBlocked()
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

            userLookupMock.Reset();
            userLookupMock.Setup(x => x.Exists(authorId)).Returns(true);
            userLookupMock.Setup(x => x.IsAuthorWritingSuspended(authorId)).Returns(true);

            var request = new CreateStoryRequestDto
            {
                Title = new string('h', 20),
                Summary = new string('i', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc15.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID15",
                spec: "Author bị chặn viết (IsAuthorWritingSuspended = true) → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID16 – thiếu ảnh bìa (Image / cover null): spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product hiện tại: <see cref="StoriesController.Create"/> chỉ upload khi <c>CoverImage</c> có dữ liệu; không có thì <c>coverUrl</c> null và vẫn gọi
        /// <see cref="StoryService.Create"/>; service gán <c>cover_image</c> null — không validate bắt buộc ảnh. Test FAIL cho đến khi product yêu cầu cover.
        /// </summary>
        [Fact]
        public void UTCID16_Create_Fail_WhenCoverImageNull()
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
                Title = new string('j', 20),
                Summary = new string('k', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING",
                CoverImage = null
            };

            // Act
            var ex = Record.Exception(() => sut.Create(request, authorId, coverImageUrl: null));
            LogTestCase(
                utcId: "UTCID16",
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
            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID17 – AgeRating không thuộc danh mục cho phép: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Độ tuổi không tồn tại" — không assert đúng từng chữ.
        /// Product: whitelist <c>ALL</c>, <c>13+</c>, <c>16+</c>, <c>18+</c> (so khớp không phân biệt hoa thường) → giá trị khác ném <see cref="ArgumentException"/>.
        /// </summary>
        [Fact]
        public void UTCID17_Create_Fail_WhenAgeRatingInvalid()
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
                Title = new string('m', 20),
                Summary = new string('n', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "21+",
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc17.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID17",
                spec: "AgeRating không thuộc whitelist → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID18 – AgeRating null: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product: <c>validAgeRatings.Contains(request.AgeRating?.ToUpper())</c> với null → false → <see cref="ArgumentException"/> (message tiếng Anh về invalid age),
        /// không phải thông báo "thiếu field" riêng nhưng vẫn chặn Create và không Add.
        /// </summary>
        [Fact]
        public void UTCID18_Create_Fail_WhenAgeRatingNull()
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
                Title = new string('o', 20),
                Summary = new string('p', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = null!,
                StoryProgressStatus = "ONGOING"
            };

            // Act
            var coverUrl = "https://example.com/covers/utc18.jpg";
            var ex = Record.Exception(() => sut.Create(request, authorId, coverUrl));
            LogTestCase(
                utcId: "UTCID18",
                spec: "AgeRating = null → fail, không Add.",
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
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID19 – truyện mới (chưa xuất bản, <c>DRAFT</c>) không được chọn tiến độ <c>HIATUS</c> (tạm ngưng): spec yêu cầu fail.
        /// Ma trận có thể ghi "Truyện chưa được xuất bản không thể chọn trạng thái là tạm ngưng" — không assert đúng từng chữ.
        /// Product: <see cref="StoryService.Create"/> chỉ cho <c>ONGOING</c> lúc tạo mới; <c>HIATUS</c> → <see cref="InvalidOperationException"/>.
        /// </summary>
        [Fact]
        public void UTCID19_Create_Fail_WhenHiatusOnUnpublishedNewStory()
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

            var title = new string('q', 20);
            var summary = new string('r', 52);
            var coverImageUrl = "https://example.com/covers/utc20.jpg";

            var request = new CreateStoryRequestDto
            {
                Title = title,
                Summary = summary,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "  HIATUS  "
            };

            // Act
            var ex = Record.Exception(() => sut.Create(request, authorId, coverImageUrl));
            LogTestCase(
                utcId: "UTCID19",
                spec: "Truyện mới không được chọn HIATUS → fail, không Add.",
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

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();

            LogStoryStore("UTCID19 (sau verify)", storyStore);
        }
    }

}



// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateStory" --logger "console;verbosity=detailed"