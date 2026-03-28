using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
            _output.WriteLine($"======== {utcId} | UT01 CreateStory ========");
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
                "Input: Title/Summary >= 50 ký tự; StoryProgressStatus ONGOING (Đang sáng tác); AgeRating 13+; CategoryIds có 1 id; coverImageUrl hợp lệ.",
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

            var title = new string('a', 52);
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
        /// Product hiện tại: slug = GenerateSlug(Title) và GetBySlug phải unique → hai lần cùng Title thường cùng slug → lần Create thứ 2 có thể throw InvalidOperationException.
        /// Test assert theo spec (hai lần đều success, 2 story, cùng Title); nếu FAIL thì product chưa khớp rule “cho phép trùng tên” ở tầng slug/DB.
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

            var sharedTitle = new string('h', 52);
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
        /// UTCID05 – StoryProgressStatus = null: spec yêu cầu fail (bắt buộc có trạng thái), không tạo story.
        /// Product hiện tại: <c>request.StoryProgressStatus ?? "ONGOING"</c> → null được mặc định thành ONGOING → vẫn tạo được.
        /// Test assert theo spec (phải có exception + không Add); hiện FAIL cho đến khi product reject null/empty status.
        /// Không assert đúng từng chữ message ("Vui lòng điền đầy đủ thông tin").
        /// </summary>
        [Fact]
        public void UTCID05_CreateStory_Fails_WhenStoryProgressStatusIsNull()
        {
            LogUtcContext("UTCID05",
                "Spec: Status/tiến độ truyện null → fail, không tạo story.",
                "Input: StoryProgressStatus = null; các field khác hợp lệ.",
                "Kỳ vọng spec: exception + không Add.",
                "Ghi chú: product có thể mặc định null → ONGOING (test sẽ FAIL).");

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
                StoryProgressStatus = null!
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
        /// UTCID08 – Summary = null: spec yêu cầu fail, không tạo story (bắt buộc có mô tả).
        /// Product hiện tại: không validate Summary; gán thẳng <c>request.Summary</c> → vẫn Add và trả DTO.
        /// Test assert theo spec (phải có exception + không Add); hiện FAIL cho đến khi product validate required summary.
        /// Không assert đúng từng chữ ("Vui lòng điền đầy đủ thông tin").
        /// </summary>
        [Fact]
        public void UTCID08_CreateStory_Fails_WhenSummaryIsNull()
        {
            LogUtcContext("UTCID08",
                "Spec: Summary null → fail, không tạo story.",
                "Input: Summary = null; title, category, age, status, cover hợp lệ.",
                "Kỳ vọng spec: exception + không Add.",
                "Ghi chú: Create hiện không check Summary — test có thể FAIL.");

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
                Title = new string('r', 24),
                Summary = null,
                CategoryIds = new List<Guid> { categoryId },
                AgeRating = "13+",
                StoryProgressStatus = "ONGOING"
            };

            var ex = Record.Exception(() => sut.Create(request, authorId, "https://example.com/covers/utc08.jpg"));
            Assert.NotNull(ex);

            Assert.Empty(storyStore);
            storyRepoMock.Verify(x => x.Add(It.IsAny<stories>(), It.IsAny<IEnumerable<Guid>>()), Times.Never);
            chapterRepoMock.VerifyNoOtherCalls();
        }
    }
}



//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT01_FunctionCreateStory"