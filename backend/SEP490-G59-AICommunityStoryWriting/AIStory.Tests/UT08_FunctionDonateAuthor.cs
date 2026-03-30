using Services.DTOs.Notifications;
using Moq;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT08_FunctionDonateAuthor
{
    private readonly ITestOutputHelper _output;

    public UT08_FunctionDonateAuthor(ITestOutputHelper output)
    {
        _output = output;
    }

    private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
    {
        _output.WriteLine("");
        _output.WriteLine($"======== {utcId} | UT08 DonateAuthor ========");
        _output.WriteLine(oneLineGoal);
        foreach (var line in details)
            _output.WriteLine("  · " + line);
    }

    private void LogActualMessage(string message)
    {
        var line = "Actual log message: " + message;
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    private void LogActualReturn<T>(T value)
    {
        var line = "Actual return: " + JsonSerializer.Serialize(value);
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    [Fact]
    public async Task UTCID01_Donate_Fails_WhenAmountIsNotPositive()
    {
        LogUtcContext("UTCID01",
            "Abnormal path: amount <= 0.",
            "Precondition: gọi DonateAsync với amount = 0.",
            "Kỳ vọng: throw ArgumentOutOfRangeException.");

        using var scope = CoinPaymentTestHelpers.CreateScope();

        var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            scope.Sut.DonateAsync(Guid.NewGuid(), Guid.NewGuid(), 0, null));

        Assert.Equal("amount", ex.ParamName);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID02_Donate_Fails_WhenSenderDonatesToSelf()
    {
        LogUtcContext("UTCID02",
            "Abnormal path: user tự donate cho chính mình.",
            "Precondition: senderUserId == receiverUserId.",
            "Kỳ vọng: throw InvalidOperationException Bạn không thể tự ủng hộ chính mình.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.DonateAsync(userId, userId, 10, null));

        Assert.Equal("Bạn không thể tự ủng hộ chính mình.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID03_Donate_Fails_WhenSenderDoesNotExist()
    {
        LogUtcContext("UTCID03",
            "Abnormal path: sender không tồn tại.",
            "Precondition: chỉ có receiver trong DB.",
            "Kỳ vọng: throw InvalidOperationException Tài khoản người ủng hộ không tồn tại.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, receiver);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.DonateAsync(Guid.NewGuid(), receiver.id, 10, null));

        Assert.Equal("Tài khoản người ủng hộ không tồn tại.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID04_Donate_Fails_WhenReceiverDoesNotExist()
    {
        LogUtcContext("UTCID04",
            "Abnormal path: receiver không tồn tại.",
            "Precondition: chỉ có sender trong DB.",
            "Kỳ vọng: throw InvalidOperationException Tác giả nhận ủng hộ không tồn tại.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.DonateAsync(sender.id, Guid.NewGuid(), 10, "Thanks"));

        Assert.Equal("Tác giả nhận ủng hộ không tồn tại.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID05_Donate_Fails_WhenSenderBalanceInsufficient()
    {
        LogUtcContext("UTCID05",
            "Abnormal path: sender không đủ coin.",
            "Precondition: sender balance = 20 nhưng donate 50.",
            "Kỳ vọng: throw InvalidOperationException Số dư coin không đủ để thực hiện ủng hộ.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader@example.com");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author@example.com");
        var senderWallet = CoinPaymentTestHelpers.CreateWallet(sender.id, balanceCoin: 20);
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver, senderWallet);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.DonateAsync(sender.id, receiver.id, 50, null));

        Assert.Equal("Số dư coin không đủ để thực hiện ủng hộ.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID06_Donate_Succeeds_WhenInputValid_AndSplitsThirtySeventy()
    {
        LogUtcContext("UTCID06",
            "Happy path: donate thành công, trừ coin sender, cộng income receiver, cộng fee vào platform.",
            "Precondition: sender và receiver tồn tại; sender có đủ balance; platform wallet có thể được tạo.",
            "Kỳ vọng: tạo donation, author_income_logs, notifications và push realtime.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader@example.com", nickname: "Reader One");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author@example.com", nickname: "Author One");
        var senderWallet = CoinPaymentTestHelpers.CreateWallet(sender.id, balanceCoin: 500);

        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver, senderWallet);
        scope.NotificationHubNotifierMock
            .Setup(x => x.NotifyUserAsync(receiver.id, It.IsAny<NotificationDto>()))
            .Returns(Task.CompletedTask);

        var result = await scope.Sut.DonateAsync(sender.id, receiver.id, 100, "Thanks");

        var savedSenderWallet = scope.DbContext.wallets.Single(x => x.user_id == sender.id);
        var savedReceiverWallet = scope.DbContext.wallets.Single(x => x.user_id == receiver.id);
        var platformWallet = scope.DbContext.platform_wallet.Single(x => x.id == 1);
        var donation = scope.DbContext.donations.Single(x => x.id == result.DonationId);
        var incomeLog = scope.DbContext.author_income_logs.Single(x => x.author_id == receiver.id);
        var notification = scope.DbContext.notifications.Single(x => x.user_id == receiver.id);

        Assert.Equal(400, savedSenderWallet.balance_coin);
        Assert.Equal(70m, savedReceiverWallet.income_balance);
        Assert.Equal(30, platformWallet.balance_coin);
        Assert.Equal(100, donation.amount);
        Assert.Equal(70m, incomeLog.net_amount);
        Assert.Equal(30m, incomeLog.platform_fee);
        Assert.Contains("Reader One", notification.content);
        scope.NotificationHubNotifierMock.Verify(x => x.NotifyUserAsync(receiver.id, It.Is<NotificationDto>(n =>
            n.Type == "DONATION" &&
            n.LinkUrl == "/wallet" &&
            n.Content != null &&
            n.Content.Contains("100 coin"))), Times.Once);

        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID07_Donate_Fails_WhenUsersExistButWalletsAreMissingAndSenderStartsAtZero()
    {
        LogUtcContext("UTCID07",
            "Boundary path: users tồn tại nhưng chưa có wallet nào.",
            "Precondition: sender/receiver đều tồn tại, nhưng wallets chưa được tạo.",
            "Kỳ vọng: service tự tạo wallet 0 coin rồi kết thúc bằng lỗi không đủ số dư.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader-boundary@example.com");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author-boundary@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.DonateAsync(sender.id, receiver.id, 100, "Thanks"));

        Assert.Equal("Số dư coin không đủ để thực hiện ủng hộ.", ex.Message);
        Assert.Equal(2, scope.DbContext.wallets.Count());
        Assert.All(scope.DbContext.wallets.ToList(), wallet => Assert.Equal(0, wallet.balance_coin));
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task NonReport_Donate_Succeeds_EvenWhenRealtimeNotificationFails()
    {
        LogUtcContext("NON-REPORT",
            "Resilience path: push realtime lỗi nhưng giao dịch donate vẫn thành công.",
            "Precondition: sender có đủ coin; notifier ném exception.",
            "Kỳ vọng: donation vẫn được lưu, ví vẫn cập nhật.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader2@example.com");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author2@example.com");
        var senderWallet = CoinPaymentTestHelpers.CreateWallet(sender.id, balanceCoin: 200);
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver, senderWallet);

        scope.NotificationHubNotifierMock
            .Setup(x => x.NotifyUserAsync(receiver.id, It.IsAny<NotificationDto>()))
            .ThrowsAsync(new InvalidOperationException("SignalR offline"));

        var result = await scope.Sut.DonateAsync(sender.id, receiver.id, 50, "Keep going");

        var savedSenderWallet = scope.DbContext.wallets.Single(x => x.user_id == sender.id);
        var savedReceiverWallet = scope.DbContext.wallets.Single(x => x.user_id == receiver.id);
        Assert.Equal(150, savedSenderWallet.balance_coin);
        Assert.Equal(35m, savedReceiverWallet.income_balance);
        Assert.Equal(1, scope.DbContext.donations.Count());
        Assert.Equal(1, scope.DbContext.notifications.Count());
        LogActualReturn(result);
    }
}
