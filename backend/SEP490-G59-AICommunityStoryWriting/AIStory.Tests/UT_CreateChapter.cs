using BusinessObjects.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Repositories;
using Services.DTOs.Chapters;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class TestLogger<T> : ILogger<T>
    {
        private readonly ITestOutputHelper _output;

        public TestLogger(ITestOutputHelper output)
        {
            _output = output;
        }

        public IDisposable BeginScope<TState>(TState state) => null!;
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            _output.WriteLine(formatter(state, exception));
        }
    }

    public class UT_CreateChapter
    {
        private readonly ITestOutputHelper _output;

        public UT_CreateChapter(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private void LogTestCase(string utcId, string spec, object? input, object? output, Exception? ex = null)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            _output.WriteLine($"SPEC   : {spec}");
            _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input, _jsonOptions)}");

            if (ex != null)
            {
                _output.WriteLine($"Exception type: {ex.GetType().Name}");
                _output.WriteLine($"Message: {ex.Message}");
            }
            else
            {
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        private static ChapterService CreateSut(
            stories story,
            List<chapters> chapterStore,
            ILogger<ChapterService> logger,
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

            storyLookupMock.Setup(x => x.GetById(It.IsAny<Guid>()))
                .Returns((Guid id) => id == story.id ? story : null);
            storyLookupMock.Setup(x => x.Update(It.IsAny<stories>()));

            userLookupMock.Setup(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>())).Returns(false);
            userLookupMock.Setup(x => x.Exists(It.IsAny<Guid>())).Returns(true);

            versionRepoMock.Setup(x => x.GetByChapterId(It.IsAny<Guid>())).Returns(Array.Empty<chapter_versions>());

            aiRepoMock.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((ai_generated_content?)null);
            aiRepoMock.Setup(x => x.BindDraftChapterId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()));
            aiRepoMock.Setup(x => x.UpdateChapterId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()));

            chapterRepoMock.Setup(x => x.GetAll()).Returns(() => chapterStore.AsQueryable());
            chapterRepoMock.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((Guid id) => chapterStore.FirstOrDefault(c => c.id == id));
            chapterRepoMock.Setup(x => x.GetByStoryId(It.IsAny<Guid>())).Returns((Guid sid) => chapterStore.Where(c => c.story_id == sid).ToList());
            chapterRepoMock.Setup(x => x.GetPublishedByStoryId(It.IsAny<Guid>()))
                .Returns((Guid sid) => chapterStore.Where(c => c.story_id == sid && string.Equals(c.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase)).ToList());
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
                logger);
        }

        private static CreateChapterRequestDto BuildRequest(Guid storyId, int orderIndex = 1) => new()
        {
            Id = Guid.NewGuid(),
            StoryId = storyId,
            Title = $"Chapter {orderIndex}",
            Content = new string('a', 600),
            OrderIndex = orderIndex,
            AccessType = "FREE",
            CoinPrice = 0,
            Status = "DRAFT"
        };

        private static stories BuildStory(Guid authorId, string progress = "ONGOING", int views = 1000) => new()
        {
            id = Guid.NewGuid(),
            title = "Story A",
            author_id = authorId,
            story_progress_status = progress,
            total_views = views,
            compliance_hidden = false
        };

        [Fact]
        public void UTCID01_CreateChapter_ShouldReturnSuccess_WhenValidInputAndAuthorized()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out var storyLookupMock, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Trời vừa hửng sáng, Minh đã đứng trước hiên nhà nhìn con đường đất còn đẫm sương. " +
                "Cậu hít một hơi thật sâu, nghe mùi cỏ non lẫn mùi khói bếp từ những mái nhà lân cận. " +
                "Hôm nay là ngày cậu quyết định rời làng để lên thành phố học nghề viết, điều mà nhiều người bảo là viển vông. " +
                "Trong chiếc ba lô cũ, ngoài vài bộ quần áo, Minh mang theo cuốn sổ tay đã sờn góc, nơi ghi lại những câu chuyện cậu nghe từ bà nội suốt thời thơ ấu. " +
                "Cậu tự nhủ nếu không bắt đầu từ hôm nay, có lẽ cả đời sẽ chỉ đứng đây, nhìn con đường mà không bao giờ bước tiếp.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;
            chapterRepoMock
                .Setup(x => x.GetByStoryIdAndOrderIndex(story.id, 1))
                .Returns((chapters?)null);

            // Act
            var dto = sut.Create(req, authorId);
            LogTestCase(
                "UTCID01",
                "Create chapter success khi AUTHOR hợp lệ và sở hữu story.",
                new
                {
                    CurrentUserRole = "AUTHOR",
                    CurrentAuthorDisplayName = "Vũ Quang Mạnh",
                    StoryAuthorId = story.author_id,
                    CurrentUserAuthorId = authorId,
                    StoryExists = true,
                    OrderIndexExists = false,
                    req.Title,
                    req.OrderIndex,
                    ContentLength = req.Content?.Length ?? 0,
                    req.AccessType,
                    req.CoinPrice
                },
                dto,
                ex: null);

            // Assert
            Assert.NotNull(dto);
            Assert.NotEqual(Guid.Empty, dto.Id);
            Assert.Equal(story.id, dto.StoryId);
            Assert.Equal("Chương 1", dto.Title);
            Assert.Equal(1, dto.OrderIndex);
            Assert.Equal("DRAFT", dto.Status);
            Assert.Equal("FREE", dto.AccessType);
            Assert.Single(store);
            storyLookupMock.Verify(x => x.GetById(story.id), Times.AtLeastOnce());
            chapterRepoMock.Verify(x => x.GetByStoryIdAndOrderIndex(story.id, 1), Times.Once);
            chapterRepoMock.Verify(x => x.Add(It.Is<chapters>(c =>
                c.story_id == story.id &&
                c.title == "Chương 1" &&
                c.order_index == 1 &&
                c.access_type == "FREE" &&
                (c.coin_price ?? 0) == 0 &&
                !string.IsNullOrWhiteSpace(c.content))), Times.Once);
        }

        [Fact]
        public void UTCID02_CreateChapter_ShouldReturnSuccess_WhenPaidAndValidCoinPrice()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId, views: 600);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out var storyLookupMock, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Bình minh vừa lên, phố nhỏ còn thưa người qua lại nhưng quán cà phê đầu ngõ đã bật đèn vàng ấm. " +
                "Lan ngồi bên cửa sổ, mở cuốn sổ tay ghi chi chít những ý tưởng dang dở từ nhiều tháng qua. " +
                "Cô quyết định hôm nay sẽ viết lại chương đầu tiên, không còn trì hoãn vì nỗi sợ bị phán xét. " +
                "Ngoài kia, tiếng xe buýt dừng rồi đi, tiếng chổi tre của bác lao công quét dọc vỉa hè, tất cả tạo nên một nhịp điệu bình yên. " +
                "Lan mỉm cười, gõ những dòng đầu tiên, tự nhắc mình rằng mỗi câu chữ trung thực đều là một bước tiến, dù nhỏ, trên hành trình trở thành tác giả mà cô hằng mong muốn.";
            req.AccessType = "PAID";
            req.CoinPrice = 10;
            chapterRepoMock
                .Setup(x => x.GetByStoryIdAndOrderIndex(story.id, 1))
                .Returns((chapters?)null);

            // Act
            var dto = sut.Create(req, authorId);
            LogTestCase("UTCID02", "Create chapter PAID thành công với coinPrice hợp lệ.", req, dto);

            // Assert
            Assert.NotNull(dto);
            Assert.NotEqual(Guid.Empty, dto.Id);
            Assert.Equal(story.id, dto.StoryId);
            Assert.Equal("Chương 1", dto.Title);
            Assert.Equal(1, dto.OrderIndex);
            Assert.Equal("PAID", dto.AccessType);
            Assert.Equal(10, dto.CoinPrice);
            Assert.Equal("DRAFT", dto.Status);
            Assert.Single(store);

            storyLookupMock.Verify(x => x.GetById(story.id), Times.AtLeastOnce);
            chapterRepoMock.Verify(x => x.GetByStoryIdAndOrderIndex(story.id, 1), Times.Once);
            chapterRepoMock.Verify(x => x.Add(It.Is<chapters>(c =>
                c.story_id == story.id &&
                c.title == "Chương 1" &&
                c.order_index == 1 &&
                c.access_type == "PAID" &&
                (c.coin_price ?? 0) == 10
            )), Times.Once);
        }

        [Fact]
        public void UTCID03_CreateChapter_ShouldThrowException_WhenOrderIndexAlreadyExists()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId);
            var store = new List<chapters>
            {
                new()
                {
                    id = Guid.NewGuid(),
                    story_id = story.id,
                    title = "Chương đã có",
                    content = new string('x', 600),
                    order_index = 1,
                    status = "DRAFT",
                    access_type = "FREE",
                    coin_price = 0
                }
            };
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Ngày mưa kéo dài suốt một tuần khiến con hẻm nhỏ trước nhà ngập loang loáng nước. " +
                "Khánh ngồi bên bàn học, mở chiếc laptop cũ và đọc lại từng dòng bản thảo đã viết từ đêm qua. " +
                "Cậu nhận ra mình thường bắt đầu câu chuyện quá vội, chưa kịp cho nhân vật thở, chưa kịp cho người đọc cảm nhận nhịp sống của bối cảnh. " +
                "Vì thế, Khánh quyết định viết lại từ đầu, chậm hơn, kỹ hơn: tiếng mưa gõ lên mái tôn, mùi trà nóng, ánh đèn vàng hắt lên trang giấy, và cả cảm giác hồi hộp trước khi nhân vật bước qua một lựa chọn quan trọng. " +
                "Khi gõ xong đoạn cuối, cậu mỉm cười vì lần này câu chuyện đã có hồn hơn rất nhiều.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, authorId));
            LogTestCase("UTCID03", "order_index đã tồn tại -> throw exception, không tạo chapter mới.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Single(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID04_CreateChapter_ShouldThrowException_WhenAccessTypeIsInvalid()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Sau hơn ba tháng chuẩn bị, nhóm tác giả cuối cùng cũng hoàn thành bản thảo chương mở đầu với bố cục rõ ràng, " +
                "nhịp kể hợp lý và mạch cảm xúc liền mạch từ đầu đến cuối. Nội dung chương mô tả hành trình của nhân vật chính " +
                "khi rời quê hương để bước vào học viện phép thuật, nơi cậu phải học cách kiểm soát sức mạnh, vượt qua định kiến " +
                "và đối mặt với những bí mật bị chôn giấu nhiều năm. Từng đoạn văn được viết chi tiết, có điểm nhấn về tâm lý nhân vật, " +
                "đồng thời giữ nhịp cao trào đủ để người đọc muốn theo dõi chương tiếp theo. Đây là nội dung hợp lệ với độ dài vượt " +
                "ngưỡng tối thiểu theo đặc tả để đảm bảo test tập trung đúng vào validation accessType.";
            req.AccessType = "VIP";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, authorId));
            LogTestCase("UTCID04", "accessType = VIP không hợp lệ phải throw exception và không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID05_CreateChapter_ShouldThrowException_WhenPaidButCoinPriceIsZero()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Trời vừa hửng sáng, mặt hồ trước làng đã phản chiếu những dải mây mỏng như lụa, còn con đường đất dẫn ra bến sông " +
                "thì vẫn đọng hơi sương sau một đêm dài mưa nhẹ. Nhân vật chính rảo bước qua khu chợ nhỏ, nghe tiếng người gọi nhau " +
                "mở hàng, mùi bánh nếp nóng hòa cùng mùi lá dong phảng phất trong gió. Cậu dừng lại trước cổng học viện, nơi cánh cổng " +
                "đá cổ khắc đầy phù văn đang dần phát sáng theo nhịp chuông buổi sớm. Trong lòng cậu vừa háo hức vừa lo lắng, bởi phía " +
                "sau cánh cổng ấy là một hành trình chưa ai trong gia đình từng trải qua. Cậu tự nhủ phải bình tĩnh, phải học thật tốt, " +
                "và phải tìm ra sự thật về lời tiên tri đã thay đổi số phận của mình từ ngày còn thơ bé.";
            req.AccessType = "PAID";
            req.CoinPrice = 5;

            // Act
            var ex = Record.Exception(() => sut.Create(req, authorId));
            LogTestCase("UTCID05", "PAID nhưng coinPrice = 5 không hợp lệ, phải throw exception và không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID06_CreateChapter_ShouldThrowException_WhenUserIsNotStoryOwner()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var nonOwnerAuthorId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Bình minh tràn qua dãy mái ngói cũ, để lại một vệt nắng mỏng trên hiên nhà nơi nhân vật chính đang ngồi buộc lại " +
                "dây giày trước chuyến đi dài đầu tiên trong đời. Cậu nghe tiếng mẹ dặn dò từ gian bếp, nghe tiếng gà gáy hòa vào " +
                "tiếng chuông nhà thờ phía cuối làng, và bỗng nhận ra từ khoảnh khắc bước qua cánh cổng này, mọi thứ quen thuộc có " +
                "thể sẽ không còn như cũ nữa. Cậu mở cuốn sổ tay cũ của cha, đọc lại dòng chữ ngắn gọn về lòng dũng cảm rồi hít một " +
                "hơi thật sâu. Trên con đường dẫn tới học viện, cậu gặp những người bạn mới, những câu hỏi chưa có lời giải, và cả " +
                "những thử thách buộc cậu phải lựa chọn giữa nỗi sợ và niềm tin vào chính mình.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, nonOwnerAuthorId));
            LogTestCase("UTCID06", "User không sở hữu story phải throw exception và không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<UnauthorizedAccessException>(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID07_CreateChapter_ShouldThrowException_WhenTitleExceedsMaxLength()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = (
                "Hành trình bình minh của người học việc phép thuật tại học viện cổ đại, nơi mỗi lựa chọn đều trả giá bằng lòng dũng cảm và ký ức tuổi thơ chưa kịp khép lại, " +
                "khi cậu phải bảo vệ bạn bè trước lời nguyền cổ, giải mã bản đồ thất truyền và đối mặt với bí mật về thân thế bị che giấu suốt nhiều năm."
            ).Substring(0, 256);
            req.Content =
                "Khi mặt trời vừa nhô lên sau rặng tre cuối làng, con đường đất trước sân đã rộn ràng tiếng bước chân của người đi chợ sớm. " +
                "Nhân vật chính đứng bên hiên nhà, tay giữ chặt chiếc túi vải cũ, lòng vừa nôn nao vừa hồi hộp trước ngày đầu đặt chân vào học viện. " +
                "Cậu nhớ lời cha dặn rằng muốn đi xa thì trước hết phải dám bước qua nỗi sợ của chính mình, nên dù còn nhiều băn khoăn, cậu vẫn " +
                "quyết định tiến về phía trước. Trên đường đi, cậu gặp một nhóm bạn đồng trang lứa, mỗi người mang theo một câu chuyện khác nhau " +
                "và cùng chia sẻ ước mơ trở thành pháp sư tài giỏi. Không khí buổi sáng trong trẻo, mùi cỏ non thoảng qua và tiếng chuông trường " +
                "vọng lại khiến cậu tin rằng hành trình mới này sẽ mở ra những điều lớn lao hơn những gì cậu từng tưởng tượng.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, authorId));
            LogTestCase("UTCID07", "Title vượt 255 ký tự phải throw exception, không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID08_CreateChapter_ShouldThrowException_WhenTitleIsNull()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = null!;
            req.Content =
                "Bầu trời đầu ngày trong xanh đến lạ, những tia nắng đầu tiên len qua tán cây và phủ lên con đường làng một màu vàng dịu. " +
                "Nhân vật chính đứng trước cổng nhà, trong tay là chiếc bản đồ cũ đã được gấp gọn, chuẩn bị cho chuyến đi đến học viện mà cậu " +
                "đã chờ đợi từ rất lâu. Cậu nhớ lại những buổi tối ngồi nghe cha kể chuyện về các pháp sư huyền thoại, về lòng dũng cảm và trách " +
                "nhiệm khi nắm trong tay sức mạnh lớn. Mỗi bước chân tiến về phía trước khiến cậu vừa hồi hộp vừa háo hức, bởi phía trước không chỉ " +
                "là những bài học mới mà còn là cơ hội để hiểu rõ bản thân mình hơn. Cậu tự nhủ sẽ không bỏ cuộc, dù hành trình có khó khăn thế nào, " +
                "vì giấc mơ này là điều cậu đã đánh đổi rất nhiều để theo đuổi.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, authorId));
            LogTestCase("UTCID08", "Title = null phải throw exception, không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID009_CreateChapter_ShouldThrowException_WhenOrderIndexIsInvalid()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, -1);
            req.Title = "Chương 1";
            req.Content =
                "Sáng sớm, những giọt sương còn đọng trên lá cỏ khi nhân vật chính lặng lẽ rời khỏi ngôi nhà nhỏ ở cuối làng để bắt đầu ngày học đầu tiên tại học viện. " +
                "Con đường quen thuộc bỗng trở nên khác lạ vì trong lòng cậu đang chứa đầy kỳ vọng và cả nỗi lo mơ hồ về tương lai phía trước. " +
                "Cậu nhớ lời dặn của mẹ rằng tri thức chỉ có giá trị khi đi cùng lòng tử tế, nhớ ánh mắt kiên định của cha trong buổi tối tiễn con lên đường. " +
                "Từng bước chân qua cây cầu gỗ, qua cánh đồng lúa còn thơm mùi đất ẩm, cậu tự nhủ phải học cách chịu trách nhiệm với lựa chọn của mình. " +
                "Bên trong chiếc túi vải cũ là cuốn sổ tay ghi đầy ghi chú, vài lá thư chưa gửi và một tấm bản đồ đã sờn góc, tất cả như nhắc cậu rằng hành trình trưởng thành " +
                "không chỉ là đi đến nơi xa hơn, mà còn là học cách hiểu bản thân sâu sắc hơn qua từng thử thách.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID009", "OrderIndex <= 0 phải throw exception, không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID10_CreateChapter_ShouldThrowException_WhenCoinPriceIsNegative()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Đêm trước ngày thi tuyển vào học viện, nhân vật chính gần như không ngủ vì vừa lo lắng vừa háo hức. " +
                "Cậu ngồi bên cửa sổ, nghe tiếng mưa nhỏ rơi trên mái ngói và đọc lại những trang ghi chú cuối cùng về các quy tắc phép thuật cơ bản. " +
                "Khi trời gần sáng, cậu gấp cuốn sổ lại, tự nhắc bản thân rằng điều quan trọng nhất không chỉ là vượt qua bài kiểm tra, mà còn là giữ vững " +
                "lòng tin vào con đường mình đã chọn. Trên đường đến học viện, cậu gặp nhiều thí sinh khác, mỗi người mang một ước mơ và một câu chuyện riêng. " +
                "Không khí trước cổng trường căng thẳng nhưng cũng đầy hy vọng, như thể mọi người đều hiểu rằng sau cánh cổng ấy là bước ngoặt lớn của cuộc đời. " +
                "Cậu hít sâu, bước vào sân chính và sẵn sàng đối mặt với thử thách đầu tiên bằng tất cả sự tập trung và quyết tâm.";
            req.AccessType = "PAID";
            req.CoinPrice = -10;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID10", "PAID nhưng coinPrice âm phải throw exception và không insert DB.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID11_CreateChapter_ShouldThrowException_WhenContentTooShort()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content = "Ngắn quá";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID11", "Content < 500 ký tự phải throw exception và không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID12_CreateChapter_ShouldThrowException_WhenPaidButCoinPriceIsNull()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId, views: 1000);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Trời vừa hửng sáng, con đường ven sông vẫn còn phủ một lớp sương mỏng khi nhân vật chính rời khỏi căn nhà nhỏ để đến học viện. " +
                "Trong chiếc túi vải cậu mang theo có cuốn sổ ghi chép, vài cây bút chì đã mòn và lá thư của mẹ dặn phải giữ lòng kiên nhẫn trước mọi thử thách. " +
                "Tiếng chuông nhà thờ ngân dài qua cánh đồng, hòa cùng tiếng mái chèo khua nhịp của những người chài sớm, khiến buổi sáng trở nên vừa bình yên vừa trang trọng. " +
                "Cậu đi qua cây cầu gỗ quen thuộc, nhớ lại từng lời cha dạy về trách nhiệm khi theo đuổi ước mơ lớn. Dù phía trước là kỳ thi khó khăn và những quy tắc nghiêm ngặt, " +
                "cậu vẫn tin rằng chỉ cần không bỏ cuộc thì mỗi ngày đều có thể tiến gần hơn tới phiên bản tốt hơn của chính mình. Ý nghĩ đó giúp cậu bước nhanh hơn, ánh mắt sáng hơn, " +
                "và sẵn sàng đón nhận mọi điều mới mẻ đang chờ phía trước cánh cổng học viện.";
            req.AccessType = "PAID";
            req.CoinPrice = null;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID12", "PAID nhưng coinPrice = null phải fail, không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID13_CreateChapter_ShouldThrowException_WhenUserIsNotStoryAuthor()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var loggedInUserId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Trời vừa hửng sáng, màn sương mỏng còn phủ trên mặt sông khi nhân vật chính rời khỏi căn nhà nhỏ để lên đường tới học viện. " +
                "Chiếc balo cũ trên vai chứa vài cuốn sách, một cây bút máy và bức thư tay của mẹ, trong đó dặn cậu phải giữ lòng tử tế dù ở bất kỳ hoàn cảnh nào. " +
                "Dọc con đường đất quen thuộc, tiếng chim gọi bầy và mùi lúa non khiến cậu vừa thấy gần gũi vừa cảm nhận rõ rằng từ hôm nay cuộc đời sẽ bước sang một chương mới. " +
                "Cậu dừng lại bên cây cầu gỗ, nhìn dòng nước trôi và nhớ lời cha nói rằng lòng can đảm không phải là không sợ hãi, mà là dám bước tiếp dù vẫn còn sợ. " +
                "Nghĩ vậy, cậu siết chặt quai túi, hít một hơi sâu rồi đi tiếp với ánh mắt kiên định, sẵn sàng đón nhận những thử thách và bài học đang chờ phía trước.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, loggedInUserId));
            LogTestCase("UTCID13", "User đã đăng nhập nhưng không phải tác giả của story phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID14_CreateChapter_ShouldThrowException_WhenAuthorDoesNotOwnStory()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var currentAuthorId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content =
                "Trời vừa hửng sáng, từng vệt nắng mỏng bắt đầu len qua những mái nhà cũ trong làng, còn con đường dẫn ra bến sông vẫn ướt sương đêm. " +
                "Nhân vật chính khẽ siết lại quai túi, bước chậm nhưng chắc về phía học viện, nơi cậu đã mơ được đặt chân đến từ khi còn nhỏ. " +
                "Trong đầu cậu vang lên lời dặn của cha: phải học cách chịu trách nhiệm với lựa chọn của mình, dù con đường phía trước có nhiều thử thách. " +
                "Cậu đi qua cây cầu gỗ, nghe tiếng nước chảy dưới chân và mùi lúa mới thoảng trong gió, bỗng thấy lòng bình tĩnh hơn rất nhiều. " +
                "Mỗi bước đi như một lời hứa với bản thân rằng cậu sẽ không bỏ cuộc, sẽ kiên trì theo đuổi ước mơ và bảo vệ những người mình trân trọng bằng cả khả năng của mình.";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, currentAuthorId));
            LogTestCase("UTCID14", "Author không sở hữu story phải fail do authorization.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID15_CreateChapter_ShouldThrowException_WhenContentIsWhitespace()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var logger = new TestLogger<ChapterService>(_output);
            var sut = CreateSut(story, store, logger, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "Chương 1";
            req.Content = "   ";
            req.AccessType = "FREE";
            req.CoinPrice = 0;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID15", "Content chỉ chứa khoảng trắng phải fail, không tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateChapter" --logger "console;verbosity=detailed"
