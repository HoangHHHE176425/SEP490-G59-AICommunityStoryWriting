using Moq;
using Services.DTOs.Notifications;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT08_FunctionDonateAuthor
{
    private readonly ITestOutputHelper _output;

    public UT08_FunctionDonateAuthor(ITestOutputHelper output)
    {
        _output = output;
    }

    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };

    private void LogTestCase(string utcId, string spec, object? input, object? output, Exception? ex = null)
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

    private void LogStore(string label, CoinPaymentTestStore store)
    {
        _output.WriteLine("");
        _output.WriteLine($"======== {label} - store ========");
        _output.WriteLine($"Users={store.Users.Count}, Wallets={store.Wallets.Count}, PlatformWallets={store.PlatformWallets.Count}, Donations={store.Donations.Count}, IncomeLogs={store.AuthorIncomeLogs.Count}, Notifications={store.Notifications.Count}");
        foreach (var wallet in store.Wallets)
        {
            _output.WriteLine($"  wallet user_id={wallet.user_id}, balance_coin={wallet.balance_coin}, income={wallet.income_balance}");
        }
        foreach (var donation in store.Donations)
        {
            _output.WriteLine($"  donation id={donation.id}, sender={donation.sender_id}, receiver={donation.receiver_id}, amount={donation.amount}");
        }
    }

    [Fact]
    public async Task UTCID01_Donate_Result_WhenAmountIsNotPositive()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var input = new { SenderUserId = Guid.NewGuid(), ReceiverUserId = Guid.NewGuid(), Amount = 0, Message = (string?)null };

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message));
        LogTestCase("UTCID01", "Amount <= 0 -> fail truoc khi luu donation.", input, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.Donations);
        Assert.Empty(scope.Store.Wallets);
        scope.NotificationHubNotifierMock.VerifyNoOtherCalls();
        LogStore("UTCID01 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID02_Donate_Result_WhenSenderDonatesToSelf()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var input = new { SenderUserId = userId, ReceiverUserId = userId, Amount = 10, Message = (string?)null };

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message));
        LogTestCase("UTCID02", "Sender donate cho chinh minh -> fail truoc khi luu donation.", input, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.Donations);
        Assert.Empty(scope.Store.Wallets);
        scope.NotificationHubNotifierMock.VerifyNoOtherCalls();
        LogStore("UTCID02 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID03_Donate_Result_WhenSenderDoesNotExist()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, receiver);
        var input = new { SenderUserId = Guid.NewGuid(), ReceiverUserId = receiver.id, Amount = 10, Message = (string?)null };

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message));
        LogTestCase("UTCID03", "Sender khong ton tai -> fail, khong tao donation.", input, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.Donations);
        Assert.Empty(scope.Store.Wallets);
        scope.NotificationHubNotifierMock.VerifyNoOtherCalls();
        LogStore("UTCID03 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID04_Donate_Result_WhenReceiverDoesNotExist()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender);
        var input = new { SenderUserId = sender.id, ReceiverUserId = Guid.NewGuid(), Amount = 10, Message = "Thanks" };

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message));
        LogTestCase("UTCID04", "Receiver khong ton tai -> fail, khong tao donation.", input, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.Donations);
        Assert.Empty(scope.Store.Wallets);
        scope.NotificationHubNotifierMock.VerifyNoOtherCalls();
        LogStore("UTCID04 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID05_Donate_Result_WhenSenderBalanceInsufficient()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader@example.com");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author@example.com");
        var senderWallet = CoinPaymentTestHelpers.CreateWallet(sender.id, balanceCoin: 20);
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver, senderWallet);
        var input = new { SenderUserId = sender.id, ReceiverUserId = receiver.id, Amount = 50, Message = (string?)null };

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message));
        LogTestCase("UTCID05", "Sender khong du coin -> fail, khong tao donation.", input, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.Donations);
        Assert.Equal(2, scope.Store.Wallets.Count);
        Assert.Equal(20, scope.Store.Wallets.Single(x => x.user_id == sender.id).balance_coin);
        Assert.Equal(0, scope.Store.Wallets.Single(x => x.user_id == receiver.id).balance_coin);
        scope.NotificationHubNotifierMock.VerifyNoOtherCalls();
        LogStore("UTCID05 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID06_Donate_Result_WhenInputValid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader@example.com", nickname: "Reader One");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author@example.com", nickname: "Author One");
        var senderWallet = CoinPaymentTestHelpers.CreateWallet(sender.id, balanceCoin: 500);
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver, senderWallet);
        scope.NotificationHubNotifierMock
            .Setup(x => x.NotifyUserAsync(receiver.id, It.IsAny<NotificationDto>()))
            .Returns(Task.CompletedTask);
        var input = new { SenderUserId = sender.id, ReceiverUserId = receiver.id, Amount = 100, Message = "Thanks" };

        // Act
        var result = await scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message);
        LogTestCase("UTCID06", "Donate hop le -> tru coin sender, cong income author, tao donation/log/notification.", input, result);

        // Assert
        var savedSenderWallet = Assert.Single(scope.Store.Wallets, x => x.user_id == sender.id);
        var savedReceiverWallet = Assert.Single(scope.Store.Wallets, x => x.user_id == receiver.id);
        var platformWallet = Assert.Single(scope.Store.PlatformWallets, x => x.id == 1);
        var donation = Assert.Single(scope.Store.Donations, x => x.id == result.DonationId);
        var incomeLog = Assert.Single(scope.Store.AuthorIncomeLogs, x => x.author_id == receiver.id);
        var notification = Assert.Single(scope.Store.Notifications, x => x.user_id == receiver.id);

        Assert.Equal(400, savedSenderWallet.balance_coin);
        Assert.Equal(30m, savedReceiverWallet.income_balance);
        Assert.Equal(70, platformWallet.balance_coin);
        Assert.Equal(100, donation.amount);
        Assert.Equal(30m, incomeLog.net_amount);
        Assert.Equal(70m, incomeLog.platform_fee);
        Assert.Contains("Reader One", notification.content);
        scope.NotificationHubNotifierMock.Verify(x => x.NotifyUserAsync(receiver.id, It.Is<NotificationDto>(n =>
            n.Type == "DONATION" &&
            n.LinkUrl == "/wallet" &&
            n.Content != null &&
            n.Content.Contains("100 coin"))), Times.Once);
        LogStore("UTCID06 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID07_Donate_Result_WhenUsersExistButWalletsAreMissing()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader-boundary@example.com");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author-boundary@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver);
        var input = new { SenderUserId = sender.id, ReceiverUserId = receiver.id, Amount = 100, Message = "Thanks" };

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message));
        LogTestCase("UTCID07", "Users ton tai nhung chua co wallet -> tao wallet 0 roi fail vi khong du coin.", input, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.Donations);
        Assert.Equal(2, scope.Store.Wallets.Count);
        Assert.All(scope.Store.Wallets, wallet => Assert.Equal(0, wallet.balance_coin));
        scope.NotificationHubNotifierMock.VerifyNoOtherCalls();
        LogStore("UTCID07 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task NonReport_Donate_Result_WhenRealtimeNotificationFails()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var sender = CoinPaymentTestHelpers.CreateUser(email: "reader2@example.com");
        var receiver = CoinPaymentTestHelpers.CreateUser(email: "author2@example.com");
        var senderWallet = CoinPaymentTestHelpers.CreateWallet(sender.id, balanceCoin: 200);
        CoinPaymentTestHelpers.Seed(scope.DbContext, sender, receiver, senderWallet);
        scope.NotificationHubNotifierMock
            .Setup(x => x.NotifyUserAsync(receiver.id, It.IsAny<NotificationDto>()))
            .ThrowsAsync(new InvalidOperationException("SignalR offline"));
        var input = new { SenderUserId = sender.id, ReceiverUserId = receiver.id, Amount = 50, Message = "Keep going" };

        // Act
        var result = await scope.Sut.DonateAsync(input.SenderUserId, input.ReceiverUserId, input.Amount, input.Message);
        LogTestCase("NON-REPORT", "Realtime notification fail nhung giao dich donate van thanh cong.", input, result);

        // Assert
        var savedSenderWallet = Assert.Single(scope.Store.Wallets, x => x.user_id == sender.id);
        var savedReceiverWallet = Assert.Single(scope.Store.Wallets, x => x.user_id == receiver.id);
        Assert.Equal(150, savedSenderWallet.balance_coin);
        Assert.Equal(15m, savedReceiverWallet.income_balance);
        Assert.Single(scope.Store.Donations);
        Assert.Single(scope.Store.Notifications);
        scope.NotificationHubNotifierMock.Verify(x => x.NotifyUserAsync(receiver.id, It.IsAny<NotificationDto>()), Times.Once);
        LogStore("NON-REPORT (sau verify)", scope.Store);
    }
}
