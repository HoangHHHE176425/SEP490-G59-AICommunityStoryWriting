using Moq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT07_FunctionPayOSWebhook
{
    private readonly ITestOutputHelper _output;

    public UT07_FunctionPayOSWebhook(ITestOutputHelper output)
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
        _output.WriteLine($"Orders={store.CoinOrders.Count}, Wallets={store.Wallets.Count}, Packages={store.CoinPackages.Count}");
        foreach (var order in store.CoinOrders)
        {
            _output.WriteLine($"  order id={order.id}, status={order.status}, link={order.gateway_transaction_id}, code={order.gateway_response_code}, coins={order.coins_granted}");
        }
        foreach (var wallet in store.Wallets)
        {
            _output.WriteLine($"  wallet user_id={wallet.user_id}, balance_coin={wallet.balance_coin}");
        }
    }

    private static string BuildSignedWebhook(string paymentLinkId, string code, string signature)
    {
        return CoinPaymentTestHelpers.BuildWebhookBody(paymentLinkId, code)
            .Replace("__SIGNATURE__", signature, StringComparison.Ordinal);
    }

    [Fact]
    public async Task UTCID01_ProcessPayOSWebhook_Result_WhenSignatureMissing()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var rawBody = "{\"data\":{\"paymentLinkId\":\"plink_1\",\"code\":\"00\"}}";

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.ProcessPayOSWebhookAsync(rawBody));
        LogTestCase("UTCID01", "Webhook thieu signature -> fail, khong thay doi du lieu.", new { RawBody = rawBody }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID01 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID02_ProcessPayOSWebhook_Result_WhenDataMissing()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var rawBody = "{\"signature\":\"anything\"}";

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.ProcessPayOSWebhookAsync(rawBody));
        LogTestCase("UTCID02", "Webhook thieu data -> fail, khong thay doi du lieu.", new { RawBody = rawBody }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID02 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID03_ProcessPayOSWebhook_Result_WhenSignatureInvalid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("expected-signature");
        var rawBody = BuildSignedWebhook("plink_2", "00", "wrong-signature");

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.ProcessPayOSWebhookAsync(rawBody));
        LogTestCase("UTCID03", "Signature khong khop checksum -> fail, khong thay doi du lieu.", new { PaymentLinkId = "plink_2", Code = "00" }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()), Times.Once);
        LogStore("UTCID03 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID04_ProcessPayOSWebhook_Result_WhenPaymentLinkIdMissing()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("valid-signature");
        var rawBody = "{\"signature\":\"valid-signature\",\"data\":{\"code\":\"00\"}}";

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.ProcessPayOSWebhookAsync(rawBody));
        LogTestCase("UTCID04", "Signature hop le nhung thieu paymentLinkId -> fail, khong update order.", new { RawBody = rawBody }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()), Times.Once);
        LogStore("UTCID04 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID05_ProcessPayOSWebhook_Result_WhenNoMatchingOrder()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-ok");
        var rawBody = BuildSignedWebhook("plink_unknown", "00", "sig-ok");

        // Act
        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        LogTestCase("UTCID05", "Webhook hop le nhung khong match order nao -> ignored.", new { PaymentLinkId = "plink_unknown", Code = "00" }, result);

        // Assert
        Assert.Equal("IGNORED_UNKNOWN_ORDER", result);
        Assert.Empty(scope.Store.CoinOrders);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()), Times.Once);
        LogStore("UTCID05 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID06_ProcessPayOSWebhook_Result_WhenOrderAlreadyPaid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var userId = Guid.NewGuid();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PAID", paymentLinkId: "plink_paid");
        var wallet = CoinPaymentTestHelpers.CreateWallet(userId, balanceCoin: 500);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order, wallet);
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-paid");
        var rawBody = BuildSignedWebhook("plink_paid", "00", "sig-paid");

        // Act
        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        LogTestCase("UTCID06", "Order da PAID -> idempotent, khong cong coin lan nua.", new { PaymentLinkId = "plink_paid", Code = "00" }, result);

        // Assert
        Assert.Equal("OK_ALREADY_PAID", result);
        Assert.Equal(500, scope.Store.Wallets.Single().balance_coin);
        Assert.Equal("PAID", scope.Store.CoinOrders.Single().status);
        scope.PayOsMock.Verify(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()), Times.Once);
        LogStore("UTCID06 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID07_ProcessPayOSWebhook_Result_WhenPayOSCodeIsNotSuccess()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(Guid.NewGuid(), package.id, status: "PENDING", paymentLinkId: "plink_failed");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-failed");
        var rawBody = BuildSignedWebhook("plink_failed", "99", "sig-failed");

        // Act
        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        LogTestCase("UTCID07", "Webhook code khac 00 -> order FAILED, khong tao wallet.", new { PaymentLinkId = "plink_failed", Code = "99" }, result);

        // Assert
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == order.id);
        Assert.Equal("OK_FAILED", result);
        Assert.Equal("FAILED", savedOrder.status);
        Assert.Equal("99", savedOrder.gateway_response_code);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()), Times.Once);
        LogStore("UTCID07 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID08_ProcessPayOSWebhook_Result_WhenPaidWithoutExistingWallet()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PENDING", coinsGranted: 230, paymentLinkId: "plink_success");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-success");
        var rawBody = BuildSignedWebhook("plink_success", "00", "sig-success");

        // Act
        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        LogTestCase("UTCID08", "Webhook paid khi chua co wallet -> tao wallet va cong coin.", new { PaymentLinkId = "plink_success", Code = "00" }, result);

        // Assert
        var wallet = Assert.Single(scope.Store.Wallets, x => x.user_id == userId);
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == order.id);
        Assert.Equal("OK_PAID", result);
        Assert.Equal(230, wallet.balance_coin);
        Assert.Equal("PAID", savedOrder.status);
        Assert.Equal("00", savedOrder.gateway_response_code);
        scope.PayOsMock.Verify(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()), Times.Once);
        LogStore("UTCID08 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID09_ProcessPayOSWebhook_Result_WhenPaidWithExistingWallet()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PENDING", coinsGranted: 120, paymentLinkId: "plink_existing_wallet");
        var wallet = CoinPaymentTestHelpers.CreateWallet(userId, balanceCoin: 80);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order, wallet);
        scope.PayOsMock
            .Setup(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()))
            .Returns("sig-existing-wallet");
        var rawBody = BuildSignedWebhook("plink_existing_wallet", "00", "sig-existing-wallet");

        // Act
        var result = await scope.Sut.ProcessPayOSWebhookAsync(rawBody);
        LogTestCase("UTCID09", "Webhook paid voi wallet co san -> cong coin vao wallet hien co.", new { PaymentLinkId = "plink_existing_wallet", Code = "00" }, result);

        // Assert
        var savedWallet = Assert.Single(scope.Store.Wallets, x => x.user_id == userId);
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == order.id);
        Assert.Equal("OK_PAID", result);
        Assert.Equal(200, savedWallet.balance_coin);
        Assert.Equal("PAID", savedOrder.status);
        Assert.Equal("00", savedOrder.gateway_response_code);
        Assert.Single(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.ComputeWebhookSignature(It.IsAny<System.Text.Json.JsonElement>()), Times.Once);
        LogStore("UTCID09 (sau verify)", scope.Store);
    }
}
