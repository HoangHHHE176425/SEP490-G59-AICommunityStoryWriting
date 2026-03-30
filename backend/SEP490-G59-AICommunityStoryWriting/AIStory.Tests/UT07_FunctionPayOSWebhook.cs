using Moq;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT07_FunctionPayOSWebhook
{
    private readonly ITestOutputHelper _output;

    public UT07_FunctionPayOSWebhook(ITestOutputHelper output)
    {
        _output = output;
    }

    private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
    {
        _output.WriteLine("");
        _output.WriteLine($"======== {utcId} | UT07 PayOSWebhook ========");
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

    private void LogActualReturn(string value)
    {
        var line = "Actual return: " + JsonSerializer.Serialize(value);
        _output.WriteLine(line);
        Console.WriteLine(line);
    }

    [Fact]
    public async Task UTCID01_ProcessPayOSWebhook_Fails_WhenSignatureMissing()
    {
        LogUtcContext("UTCID01",
            "Abnormal path: webhook thiếu signature.",
            "Precondition: raw body có data nhưng không có signature.",
            "Kỳ vọng: throw InvalidOperationException Missing signature.");

        using var scope = CoinPaymentTestHelpers.CreateScope();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.ProcessPayOSWebhookAsync("{\"data\":{\"paymentLinkId\":\"plink_1\",\"code\":\"00\"}}"));

        Assert.Equal("Missing signature", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID02_ProcessPayOSWebhook_Fails_WhenDataMissing()
    {
        LogUtcContext("UTCID02",
            "Abnormal path: payload thiếu data.",
            "Precondition: raw body có signature nhưng không có property data.",
            "Kỳ vọng: throw InvalidOperationException Missing data.");

        using var scope = CoinPaymentTestHelpers.CreateScope();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.ProcessPayOSWebhookAsync("{\"signature\":\"anything\"}"));

        Assert.Equal("Missing data", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID03_ProcessPayOSWebhook_Fails_WhenSignatureInvalid()
    {
        LogUtcContext("UTCID03",
            "Abnormal path: signature không khớp checksum.",
            "Precondition: payos.ComputeWebhookSignature trả giá trị khác signature trong body.",
            "Kỳ vọng: throw InvalidOperationException Invalid signature.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("expected-signature");

        var rawBody = CoinPaymentTestHelpers.BuildWebhookBody("plink_2", "00")
            .Replace("__SIGNATURE__", "wrong-signature", StringComparison.Ordinal);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.ProcessPayOSWebhookAsync(rawBody));

        Assert.Equal("Invalid signature", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID04_ProcessPayOSWebhook_Fails_WhenPaymentLinkIdMissing()
    {
        LogUtcContext("UTCID04",
            "Abnormal path: payload không có paymentLinkId.",
            "Precondition: signature hợp lệ nhưng data thiếu paymentLinkId.",
            "Kỳ vọng: throw InvalidOperationException Missing paymentLinkId.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("valid-signature");

        var rawBody = "{\"signature\":\"valid-signature\",\"data\":{\"code\":\"00\"}}";

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.ProcessPayOSWebhookAsync(rawBody));

        Assert.Equal("Missing paymentLinkId", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID05_ProcessPayOSWebhook_ReturnsIgnoredUnknownOrder_WhenNoMatchingOrder()
    {
        LogUtcContext("UTCID05",
            "Boundary path: webhook hợp lệ nhưng không match coin order nào.",
            "Precondition: DB không có order với paymentLinkId tương ứng.",
            "Kỳ vọng: trả IGNORED_UNKNOWN_ORDER.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-ok");

        var rawBody = CoinPaymentTestHelpers.BuildWebhookBody("plink_unknown", "00")
            .Replace("__SIGNATURE__", "sig-ok", StringComparison.Ordinal);

        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);

        Assert.Equal("IGNORED_UNKNOWN_ORDER", result);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID06_ProcessPayOSWebhook_ReturnsOkAlreadyPaid_WhenOrderAlreadyPaid()
    {
        LogUtcContext("UTCID06",
            "Idempotent path: order đã PAID trước đó.",
            "Precondition: DB có order status PAID cùng paymentLinkId.",
            "Kỳ vọng: trả OK_ALREADY_PAID và không cộng coin lần nữa.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var userId = Guid.NewGuid();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PAID", paymentLinkId: "plink_paid");
        var wallet = CoinPaymentTestHelpers.CreateWallet(userId, balanceCoin: 500);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order, wallet);

        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-paid");

        var rawBody = CoinPaymentTestHelpers.BuildWebhookBody("plink_paid", "00")
            .Replace("__SIGNATURE__", "sig-paid", StringComparison.Ordinal);

        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);

        Assert.Equal("OK_ALREADY_PAID", result);
        Assert.Equal(500, scope.DbContext.wallets.Single().balance_coin);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID07_ProcessPayOSWebhook_ReturnsOkFailed_WhenPayOSCodeIsNotSuccess()
    {
        LogUtcContext("UTCID07",
            "Abnormal path: webhook hợp lệ nhưng code khác 00.",
            "Precondition: order đang PENDING.",
            "Kỳ vọng: order chuyển FAILED và trả OK_FAILED.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(Guid.NewGuid(), package.id, status: "PENDING", paymentLinkId: "plink_failed");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-failed");

        var rawBody = CoinPaymentTestHelpers.BuildWebhookBody("plink_failed", "99")
            .Replace("__SIGNATURE__", "sig-failed", StringComparison.Ordinal);

        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        var savedOrder = scope.DbContext.coin_orders.Single(x => x.id == order.id);

        Assert.Equal("OK_FAILED", result);
        Assert.Equal("FAILED", savedOrder.status);
        Assert.Equal("99", savedOrder.gateway_response_code);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID08_ProcessPayOSWebhook_ReturnsOkPaid_AndCreatesWallet_WhenWebhookPaidWithoutExistingWallet()
    {
        LogUtcContext("UTCID08",
            "Happy path: webhook code 00 thì order PAID và cộng coin vào wallet.",
            "Precondition: order PENDING, wallet chưa tồn tại.",
            "Kỳ vọng: trả OK_PAID, tạo wallet và cộng đúng số coin.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PENDING", coinsGranted: 230, paymentLinkId: "plink_success");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-success");

        var rawBody = CoinPaymentTestHelpers.BuildWebhookBody("plink_success", "00")
            .Replace("__SIGNATURE__", "sig-success", StringComparison.Ordinal);

        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        var wallet = scope.DbContext.wallets.Single(x => x.user_id == userId);
        var savedOrder = scope.DbContext.coin_orders.Single(x => x.id == order.id);

        Assert.Equal("OK_PAID", result);
        Assert.Equal(230, wallet.balance_coin);
        Assert.Equal("PAID", savedOrder.status);
        Assert.Equal("00", savedOrder.gateway_response_code);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID09_ProcessPayOSWebhook_ReturnsOkPaid_AndCreditsExistingWallet_WhenWebhookPaid()
    {
        LogUtcContext("UTCID09",
            "Happy path: webhook code 00 thì cộng coin vào wallet đã tồn tại.",
            "Precondition: order PENDING và user đã có wallet entry.",
            "Kỳ vọng: trả OK_PAID, không tạo wallet mới, balance_coin tăng đúng coins_granted.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PENDING", coinsGranted: 120, paymentLinkId: "plink_existing_wallet");
        var wallet = CoinPaymentTestHelpers.CreateWallet(userId, balanceCoin: 80);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order, wallet);

        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-existing-wallet");

        var rawBody = CoinPaymentTestHelpers.BuildWebhookBody("plink_existing_wallet", "00")
            .Replace("__SIGNATURE__", "sig-existing-wallet", StringComparison.Ordinal);

        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        var savedWallet = scope.DbContext.wallets.Single(x => x.user_id == userId);
        var savedOrder = scope.DbContext.coin_orders.Single(x => x.id == order.id);

        Assert.Equal("OK_PAID", result);
        Assert.Equal(200, savedWallet.balance_coin);
        Assert.Equal("PAID", savedOrder.status);
        Assert.Equal("00", savedOrder.gateway_response_code);
        Assert.Single(scope.DbContext.wallets);
        LogActualReturn(result);
    }
}
