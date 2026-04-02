using AIStory.API.Controllers;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Repositories;
using Services.DTOs.Stories;
using Services.Implementations;
using Services.Interfaces;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT01_FunctionCreateStory
    {
        private readonly ITestOutputHelper _output;

        public UT01_FunctionCreateStory(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
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
        /// Spec có thể nêu tên tác giả hiển thị / ảnh upload; DTO Create chỉ có authorId + cover URL sau xử lý API — không assert message log.
        /// </summary>
        [Fact]
        public void UTCID01_CreateStory_Succeeds_WhenAllInputsValid_HappyPath()
        {
            LogUtcContext("UTCID01",
                "Happy path: tất cả input hợp lệ → Create thành công, có DTO trả về và story được Add.",
                "Precondition: user Exists; không IsAuthorWritingSuspended; category tồn tại và is_active.",
                "Input: Title/Summary đủ dài trong giới hạn nghiệp vụ (title ≤ 50); StoryProgressStatus ONGOING; AgeRating 13+; CategoryIds có 1 id; coverImageUrl hợp lệ.",
                "Kỳ vọng spec: success; response có title, summary, authorId, category ids, status/progress; không assert đúng từng chữ log.");

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

            var dto = sut.Create(request, authorId, coverImageUrl);

            Assert.NotNull(dto);
            Assert.Equal(title, dto.Title);
            Assert.Equal(summary, dto.Summary);
            Assert.Equal(authorId, dto.AuthorId);
            Assert.Contains(categoryId, dto.CategoryIds);
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
        public void UTCID02_CreateStory_Succeeds_WhenTitleDuplicatesExistingStory_DisplayNameAllowed()
        {
            LogUtcContext("UTCID02",
                "Spec: trùng Title (khác author/story) vẫn tạo được — title không là unique key.",
                "Precondition: user/author hợp lệ; category hợp lệ; đã có 1 story với Title = T (hoặc tạo story đầu rồi tạo story thứ hai cùng T).",
                "Input: lần 2 giữ nguyên Title T, các field khác hợp lệ (author B, summary khác, …).",
                "Kỳ vọng spec: Success cả hai; 2 bản ghi; cùng title string; không assert message log.");

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

            var dtoA = sut.Create(requestA, authorA, coverUrl);
            var dtoB = sut.Create(requestB, authorB, coverUrl);

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
        public void UTCID03_CreateStory_Fails_WhenAuthorIdIsEmptyOrMissing()
        {
            LogUtcContext("UTCID03",
                "Spec: không có author hợp lệ → fail, không Add story.",
                "Mô phỏng: Create(req, Guid.Empty, cover) với Exists(Empty) = false.",
                "Các field khác hợp lệ (summary >= 50, category, age 13+, status ONGOING, cover URL).",
                "Kỳ vọng: exception; không assert message từng chữ.");

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

            Assert.Throws<InvalidOperationException>(() => sut.Create(request, Guid.Empty, "https://example.com/covers/x.jpg"));

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
        public void UTCID04_CreateStory_Fails_WhenStoryProgressStatusInvalid()
        {
            LogUtcContext("UTCID04",
                "Spec: Status/tiến độ truyện không thuộc tập hợp cho phép → fail.",
                "Input: StoryProgressStatus = giá trị lạ (không phải ONGOING/COMPLETED/HIATUS); các field khác hợp lệ.",
                "Kỳ vọng: exception + không Add; không assert message từng chữ.");

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

            Assert.Throws<ArgumentException>(() => sut.Create(request, authorId, "https://example.com/covers/utc04.jpg"));

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
        public void UTCID05_CreateStory_Fails_WhenStoryProgressStatusIsNull()
        {
            LogUtcContext("UTCID05",
                "Spec: Status/tiến độ truyện null → fail, không tạo story.",
                "Input: StoryProgressStatus = null; các field khác hợp lệ.",
                "Kỳ vọng: exception + không Add.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc05.jpg"));
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
        public void UTCID06_CreateStory_Fails_WhenCategoryIdDoesNotExist()
        {
            LogUtcContext("UTCID06",
                "Spec: CategoryId không map tới thể loại thật → fail.",
                "Input: CategoryIds chứa Guid không có trong lookup; các field khác hợp lệ.",
                "Kỳ vọng: exception + không Add; không assert message từng chữ.");

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

            Assert.Throws<InvalidOperationException>(() => sut.Create(request, authorId, "https://example.com/covers/utc06.jpg"));

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
        public void UTCID07_CreateStory_Fails_WhenCategoryIdsIsNull()
        {
            LogUtcContext("UTCID07",
                "Spec: CategoryId / danh sách thể loại null → fail.",
                "Input: CategoryIds = null; các field khác hợp lệ.",
                "Kỳ vọng: exception + không Add; không gọi GetById category.",
                "Ghi chú: List rỗng cũng fail cùng nhánh product — case này dùng null theo matrix.");

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

            Assert.Throws<InvalidOperationException>(() => sut.Create(request, authorId, "https://example.com/covers/utc07.jpg"));

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
        public void UTCID08_CreateStory_Succeeds_WhenSummaryIsNull()
        {
            LogUtcContext("UTCID08",
                "Summary không còn bắt buộc: null vẫn tạo story thành công.",
                "Input: Summary = null; title, category, age, status, cover hợp lệ.",
                "Kỳ vọng: DTO + Add một lần; không exception.");

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

            var dto = sut.Create(request, authorId, coverUrl);

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
        public void UTCID09_CreateStory_Fails_WhenInputExceedsMaxLength()
        {
            LogUtcContext("UTCID09",
                "Spec: ít nhất một field vượt độ dài/giới hạn cho phép → fail.",
                "Input: Title rất dài (mô phỏng vượt max); Summary hợp lệ; category/age/status/cover hợp lệ; slug chưa trùng (store rỗng).",
                "Kỳ vọng: ArgumentException + không Add.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc09.jpg"));
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
        public void UTCID10_CreateStory_Fails_WhenInputHasInvalidFormat()
        {
            LogUtcContext("UTCID10",
                "Spec: ít nhất một field sai format/pattern theo rule hệ thống → fail.",
                "Input: Title/Summary đủ dài; Category ONGOING hợp lệ; Status ONGOING; cover URL hợp lệ; slug chưa trùng.",
                "Vi phạm: AgeRating = \"PG13\" (không thuộc tập mã cho phép — mô phỏng sai định dạng so với \"13+\").",
                "Kỳ vọng spec: exception + không Add. Không assert đúng từng chữ message.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc10.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID11 – ảnh bìa upload vượt giới hạn dung lượng → không tạo story.
        /// Ma trận có thể ghi "Kích thước file quá lớn" — không assert đúng từng chữ.
        /// Product: kiểm tra ở <see cref="StoriesController.Create"/> (CoverImage.Length &gt; 5MB → BadRequest), không nằm trong <see cref="StoryService.Create"/>.
        /// </summary>
        [Fact]
        public async Task UTCID11_CreateStory_ReturnsBadRequest_WhenCoverImageFileTooLarge()
        {
            LogUtcContext("UTCID11",
                "Spec: file ảnh quá lớn → fail, không tạo story.",
                "Product: StoriesController từ chối trước khi gọi StoryService (giới hạn 5MB).",
                "Kỳ vọng: BadRequest; IStoryService.Create không được gọi. Không assert đúng từng chữ message.");

            const long maxBytes = 5L * 1024 * 1024;
            var authorId = Guid.NewGuid();

            var coverFile = new Mock<IFormFile>(MockBehavior.Strict);
            coverFile.Setup(f => f.Length).Returns(maxBytes + 1);
            coverFile.Setup(f => f.FileName).Returns("huge-cover.jpg");
            coverFile.Setup(f => f.CopyToAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

            var storyServiceMock = new Mock<IStoryService>(MockBehavior.Strict);
            var guardMock = new Mock<IContentGuardrailService>();
            var reportMock = new Mock<IStoryReportService>();
            var notifMock = new Mock<INotificationHubNotifier>();
            var commentPostMock = new Mock<IStoryCommentPostService>(MockBehavior.Strict);
            var logger = NullLogger<StoriesController>.Instance;

            var controller = new StoriesController(
                storyServiceMock.Object,
                guardMock.Object,
                reportMock.Object,
                notifMock.Object,
                commentPostMock.Object,
                logger);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, authorId.ToString()),
                new Claim(ClaimTypes.Role, "AUTHOR")
            };
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"))
                }
            };

            var categoryId = Guid.NewGuid();
            var request = new CreateStoryRequestDto
            {
                Title = new string('c', 24),
                Summary = new string('d', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING",
                CoverImage = coverFile.Object
            };

            var result = await controller.Create(request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.NotNull(badRequest.Value);

            storyServiceMock.Verify(
                s => s.Create(It.IsAny<CreateStoryRequestDto>(), It.IsAny<Guid>(), It.IsAny<string?>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID12 – Title vượt giới hạn nghiệp vụ (ma trận: &gt; 50 ký tự).
        /// </summary>
        [Fact]
        public void UTCID12_CreateStory_Fails_WhenTitleExceedsMaxLength()
        {
            LogUtcContext("UTCID12",
                "Spec: Title vượt giới hạn ký tự → fail, không tạo story.",
                "Input: Title dài hơn 50 (ma trận); Summary/category/age/status/cover hợp lệ; slug chưa trùng.",
                "Kỳ vọng: ArgumentException + không Add.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc12.jpg"));
            Assert.IsType<ArgumentException>(ex);
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID13 – Title null: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product: không có validate required Title rõ ràng; <see cref="StoryService.Create"/> gọi <c>GenerateSlug(request.Title)</c> →
        /// <c>Title == null</c> thường ném <see cref="NullReferenceException"/> trước khi Add (fail + không lưu, nhưng không phải thông báo nghiệp vụ có chủ đích).
        /// </summary>
        [Fact]
        public void UTCID13_CreateStory_Fails_WhenTitleIsNull()
        {
            LogUtcContext("UTCID13",
                "Spec: Title null → fail, không tạo story.",
                "Input: Title = null; summary/category/age/status/cover hợp lệ; slug chưa trùng.",
                "Kỳ vọng spec: exception + không Add. Không assert đúng từng chữ message.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc13.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID14 – người thực hiện không phải tác giả hợp lệ trong hệ thống: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Bạn không phải là tác giả" — không assert đúng từng chữ.
        /// Product: <see cref="StoryService.Create"/> gọi <see cref="IUserLookup.Exists"/> trước; user không tồn tại → <see cref="InvalidOperationException"/> (message hiện tại nói AuthorId không có trong bảng users).
        /// Khác UTCID03: dùng Guid không rỗng nhưng <c>Exists == false</c> (mô phỏng định danh không gắn user/tác giả thật trong DB).
        /// </summary>
        [Fact]
        public void UTCID14_CreateStory_Fails_WhenCallerIsNotRegisteredAuthor()
        {
            LogUtcContext("UTCID14",
                "Spec: user không được xác định là tác giả hợp lệ → fail, không Add.",
                "Mô phỏng: authorId = Guid có thật (không Empty) nhưng IUserLookup.Exists(authorId) = false.",
                "Input story hợp lệ; slug chưa trùng. Kỳ vọng: exception + không Add; không assert message từng chữ.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc14.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>()), Times.Never);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID15 – Title chỉ gồm khoảng trắng: spec yêu cầu fail (tương đương <c>string.IsNullOrWhiteSpace</c>), không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product hiện tại: Create không gọi <c>IsNullOrWhiteSpace(Title)</c>; slug rỗng vẫn có thể Add → test FAIL cho đến khi product validate.
        /// </summary>
        [Fact]
        public void UTCID15_CreateStory_Fails_WhenTitleIsWhitespaceOnly()
        {
            LogUtcContext("UTCID15",
                "Spec: Title chỉ chứa khoảng trắng → fail, không Add.",
                "Input: Title = spaces/tabs; summary/category/age/status/cover hợp lệ; slug chưa trùng.",
                "Kỳ vọng spec: exception + không Add. Không assert đúng từng chữ message.",
                "Ghi chú: product có thể vẫn tạo story với slug rỗng — test có thể FAIL.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc15.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID16 – tác giả bị chặn tạo nội dung (spec: BAN): fail, không tạo story.
        /// Ma trận có thể ghi "Author đã bị BAN" — không assert đúng từng chữ.
        /// Product: <see cref="StoryService.Create"/> gọi <see cref="IUserLookup.IsAuthorWritingSuspended"/>; khi true → <see cref="InvalidOperationException"/>
        /// (message hiện tại: tạm khóa chức năng viết truyện). Không có kiểm tra <c>users.status == BANNED</c> trong Create — mô phỏng "bị chặn" bằng suspend viết.
        /// </summary>
        [Fact]
        public void UTCID16_CreateStory_Fails_WhenAuthorWritingIsBlocked()
        {
            LogUtcContext("UTCID16",
                "Spec: author bị BAN / chặn tạo truyện → fail, không Add.",
                "Product: IsAuthorWritingSuspended = true (tạm khóa viết compliance/admin).",
                "Input story hợp lệ; slug chưa trùng. Kỳ vọng: exception + không Add; không assert message từng chữ.",
                "Ghi chú: trạng thái BANNED trên user có thể chưa được Create kiểm tra riêng.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc16.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(authorId), Times.Once);
            userLookupMock.Verify(x => x.IsAuthorWritingSuspended(authorId), Times.Once);
            categoryLookupMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID17 – thiếu ảnh bìa (Image / cover null): spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product hiện tại: <see cref="StoriesController.Create"/> chỉ upload khi <c>CoverImage</c> có dữ liệu; không có thì <c>coverUrl</c> null và vẫn gọi
        /// <see cref="StoryService.Create"/>; service gán <c>cover_image</c> null — không validate bắt buộc ảnh. Test FAIL cho đến khi product yêu cầu cover.
        /// </summary>
        [Fact]
        public void UTCID17_CreateStory_Fails_WhenCoverImageIsNull()
        {
            LogUtcContext("UTCID17",
                "Spec: không có ảnh đại diện (cover null) → fail, không Add.",
                "Mô phỏng API: StoryService.Create(..., coverImageUrl: null); các field khác hợp lệ; slug chưa trùng.",
                "Kỳ vọng spec: exception + không Add. Không assert đúng từng chữ message.",
                "Ghi chú: product hiện cho phép tạo truyện không ảnh bìa — test có thể FAIL.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, coverImageUrl: null));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID18 – AgeRating không thuộc danh mục cho phép: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Độ tuổi không tồn tại" — không assert đúng từng chữ.
        /// Product: whitelist <c>ALL</c>, <c>13+</c>, <c>16+</c>, <c>18+</c> (so khớp không phân biệt hoa thường) → giá trị khác ném <see cref="ArgumentException"/>.
        /// </summary>
        [Fact]
        public void UTCID18_CreateStory_Fails_WhenAgeRatingIsInvalid()
        {
            LogUtcContext("UTCID18",
                "Spec: Age không hợp lệ (ngoài tập mức độ tuổi) → fail, không Add.",
                "Input: AgeRating = \"21+\" (không có trong ALL/13+/16+/18+); các field khác hợp lệ; slug chưa trùng.",
                "Kỳ vọng: exception + không Add. Không assert đúng từng chữ message.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc18.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID19 – AgeRating null: spec yêu cầu fail, không tạo story.
        /// Ma trận có thể ghi "Vui lòng điền đầy đủ thông tin" — không assert đúng từng chữ.
        /// Product: <c>validAgeRatings.Contains(request.AgeRating?.ToUpper())</c> với null → false → <see cref="ArgumentException"/> (message tiếng Anh về invalid age),
        /// không phải thông báo "thiếu field" riêng nhưng vẫn chặn Create và không Add.
        /// </summary>
        [Fact]
        public void UTCID19_CreateStory_Fails_WhenAgeRatingIsNull()
        {
            LogUtcContext("UTCID19",
                "Spec: Age null / thiếu → fail, không Add.",
                "Input: AgeRating = null; các field khác hợp lệ; slug chưa trùng.",
                "Kỳ vọng: exception + không Add. Không assert đúng từng chữ message.");

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

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc19.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }

        /// <summary>
        /// UTCID20 – truyện mới (chưa xuất bản, <c>DRAFT</c>) không được chọn tiến độ <c>HIATUS</c> (tạm ngưng): spec yêu cầu fail.
        /// Ma trận có thể ghi "Truyện chưa được xuất bản không thể chọn trạng thái là tạm ngưng" — không assert đúng từng chữ.
        /// <b>Bug mở:</b> test assert theo spec → <b>FAIL</b> (đỏ) cho đến khi dev implement chặn HIATUS trong <see cref="StoryService.Create"/>; không dùng <c>Skip</c>.
        /// </summary>
        [Fact]
        public void UTCID20_CreateStory_Fails_WhenHiatusOnUnpublishedNewStory()
        {
            LogUtcContext("UTCID20",
                "Spec: truyện chưa xuất bản + StoryProgressStatus HIATUS → fail, không Add.",
                "Input: StoryProgressStatus = HIATUS; category/age/title/summary/cover hợp lệ; slug chưa trùng.",
                "Kỳ vọng spec: exception + không Add. Không assert đúng từng chữ message.",
                "Ghi chú: ma trận ghi HIATUS ở tiến độ truyện, không phải CategoryId.");

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
                Title = new string('q', 20),
                Summary = new string('r', 52),
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "HIATUS"
            };

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc20.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }
    }
}




//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT01_FunctionCreateStory"