using Moq;
using Services.DTOs.Payments;
using Services.Integrations.PayOS;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT06_FunctionPayOSTopup
{
    private readonly ITestOutputHelper _output;

    public UT06_FunctionPayOSTopup(ITestOutputHelper output)
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
        _output.WriteLine($"Packages={store.CoinPackages.Count}, Orders={store.CoinOrders.Count}, Wallets={store.Wallets.Count}");
        foreach (var order in store.CoinOrders)
        {
            _output.WriteLine($"  order id={order.id}, user_id={order.user_id}, package_id={order.package_id}, status={order.status}, gateway={order.payment_gateway}, link={order.gateway_transaction_id}, code={order.gateway_response_code}, coins={order.coins_granted}");
        }
        foreach (var wallet in store.Wallets)
        {
            _output.WriteLine($"  wallet user_id={wallet.user_id}, balance_coin={wallet.balance_coin}");
        }
    }

    private static CreatePayOSPaymentRequestDto CreatePaymentRequest(Guid packageId)
    {
        return new CreatePayOSPaymentRequestDto
        {
            PackageId = packageId,
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        };
    }

    private static PayOSClient.GetPaymentRequestResult PayosStatus(string paymentLinkId, string status, string? code = null)
    {
        return new PayOSClient.GetPaymentRequestResult(
            paymentLinkId,
            status,
            1,
            10000,
            string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) ? 10000 : 0,
            string.Equals(status, "PAID", StringComparison.OrdinalIgnoreCase) ? 0 : 10000,
            DateTimeOffset.UtcNow.AddMinutes(-10),
            status is "CANCELLED" or "EXPIRED" ? DateTimeOffset.UtcNow.AddMinutes(-1) : null,
            JsonSerializer.Serialize(new { status }),
            code);
    }

    [Fact]
    public async Task UTCID01_CreatePayOSPayment_Result_WhenPackageNotFound()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var request = CreatePaymentRequest(Guid.NewGuid());

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.CreatePayOSPaymentAsync(userId, request));
        LogTestCase("UTCID01", "Package khong ton tai -> fail, khong goi PayOS va khong tao coin_order.", new { UserId = userId, Request = request }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID01 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID02_CreatePayOSPayment_Result_WhenPackageInactive()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: false);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);
        var request = CreatePaymentRequest(package.id);

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.CreatePayOSPaymentAsync(userId, request));
        LogTestCase("UTCID02", "Package inactive -> fail, khong goi PayOS va khong tao coin_order.", new { UserId = userId, Request = request }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID02 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID03_CreatePayOSPayment_Result_WhenPackageCoinConfigurationInvalid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(coinAmount: 0, bonusCoin: 0);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);
        var request = CreatePaymentRequest(package.id);

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.CreatePayOSPaymentAsync(userId, request));
        LogTestCase("UTCID03", "Tong coin package <= 0 -> fail, khong tao coin_order.", new { UserId = userId, Request = request }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID03 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID04_CreatePayOSPayment_Result_WhenPackagePriceInvalid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(priceAmount: 0m, coinAmount: 100, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);
        var request = CreatePaymentRequest(package.id);

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.CreatePayOSPaymentAsync(userId, request));
        LogTestCase("UTCID04", "Gia package <= 0 -> fail, khong tao coin_order.", new { UserId = userId, Request = request }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID04 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID05_CreatePayOSPayment_Result_WhenPackageValid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(priceAmount: 12000m, coinAmount: 120, bonusCoin: 30, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);
        var request = CreatePaymentRequest(package.id);

        scope.PayOsMock
            .Setup(x => x.CreatePaymentLinkAsync(
                It.IsAny<long>(),
                12000m,
                "Nap coin: Starter Pack",
                It.Is<string>(url => url.StartsWith("https://app.test/cancel") && url.Contains("orderId=")),
                It.Is<string>(url => url.StartsWith("https://app.test/return") && url.Contains("orderId=")),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.CreatePaymentLinkResult("plink_123", "https://payos.test/checkout/123", "{\"code\":\"00\"}", "00"));

        // Act
        var result = await scope.Sut.CreatePayOSPaymentAsync(userId, request);
        LogTestCase("UTCID05", "Package hop le -> tao coin_order PENDING va tra checkout url.", new { UserId = userId, Request = request }, result);

        // Assert
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == result.CoinOrderId);
        Assert.Equal(package.id, result.PackageId);
        Assert.Equal(150, result.CoinsGranted);
        Assert.Equal("PENDING", savedOrder.status);
        Assert.Equal("PAYOS", savedOrder.payment_gateway);
        Assert.Equal("plink_123", savedOrder.gateway_transaction_id);
        Assert.Equal("00", savedOrder.gateway_response_code);
        Assert.True(result.OrderCode > 0);
        scope.PayOsMock.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), 12000m, "Nap coin: Starter Pack", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID05 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID06_CreatePayOSPayment_Result_WhenExpiredAtNeedsClamping()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope(new Dictionary<string, string?>
        {
            ["PayOS:DefaultExpiredMinutes"] = "0"
        });
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);
        var request = new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://test.local/cancel",
            ReturnUrl = "https://test.local/return"
        };
        int? capturedExpiredAt = null;
        scope.PayOsMock
            .Setup(x => x.CreatePaymentLinkAsync(
                It.IsAny<long>(),
                package.price_amount,
                $"Nap coin: {package.name}",
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .Callback<long, decimal, string, string, string, int?, CancellationToken>((_, _, _, _, _, expiredAt, _) => capturedExpiredAt = expiredAt)
            .ReturnsAsync(new PayOSClient.CreatePaymentLinkResult("plink_exp", "https://payos.test/checkout/exp", "{\"code\":\"00\"}", "00"));

        // Act
        var beforeMin = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var result = await scope.Sut.CreatePayOSPaymentAsync(userId, request);
        var afterMax = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();
        LogTestCase("UTCID06", "expiredAt gui sang PayOS nam trong range hop le sau khi clamp config.", new { UserId = userId, Request = request }, new { Result = result, ExpiredAt = capturedExpiredAt });

        // Assert
        Assert.NotNull(capturedExpiredAt);
        Assert.InRange(capturedExpiredAt.Value, (int)beforeMin - 5, (int)afterMax + 5);
        Assert.Single(scope.Store.CoinOrders, x => x.id == result.CoinOrderId);
        scope.PayOsMock.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), package.price_amount, $"Nap coin: {package.name}", It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID06 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID07_CreatePayOSPayment_Result_WhenPayOSThrows()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);
        var request = CreatePaymentRequest(package.id);
        scope.PayOsMock
            .Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PayOS error 503: maintenance"));

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.CreatePayOSPaymentAsync(userId, request));
        LogTestCase("UTCID07", "PayOS CreatePaymentLinkAsync throw -> service persist coin_order FAILED va rethrow.", new { UserId = userId, Request = request }, null, ex);

        // Assert
        Assert.NotNull(ex);
        var failed = Assert.Single(scope.Store.CoinOrders);
        Assert.Equal("FAILED", failed.status);
        Assert.Null(failed.gateway_transaction_id);
        Assert.NotNull(failed.gateway_response_code);
        scope.PayOsMock.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID07 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID08_CreatePayOSPayment_Result_WhenPayOSCodeRejected()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);
        var request = CreatePaymentRequest(package.id);
        scope.PayOsMock
            .Setup(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.CreatePaymentLinkResult("plink_x", "https://payos.test/checkout/x", "{}", "99"));

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.CreatePayOSPaymentAsync(userId, request));
        LogTestCase("UTCID08", "PayOS tra code khac 00 -> service persist coin_order FAILED va rethrow.", new { UserId = userId, Request = request }, null, ex);

        // Assert
        Assert.NotNull(ex);
        var failed = Assert.Single(scope.Store.CoinOrders);
        Assert.Equal("FAILED", failed.status);
        Assert.Null(failed.gateway_transaction_id);
        Assert.Contains("99", failed.gateway_response_code ?? string.Empty);
        scope.PayOsMock.Verify(x => x.CreatePaymentLinkAsync(It.IsAny<long>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID08 create (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID01_SyncMyPayOSOrder_Result_WhenOrderNotFound()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var orderId = Guid.NewGuid();

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.SyncMyPayOSOrderAsync(userId, orderId));
        LogTestCase("UTCID01", "coinOrderId khong ton tai -> fail, khong goi PayOS.", new { UserId = userId, CoinOrderId = orderId }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID01 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID02_SyncMyPayOSOrder_Result_WhenOrderBelongsToDifferentUser()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var ownerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(ownerId, package.id, paymentLinkId: "plink-owner");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.SyncMyPayOSOrderAsync(anotherUserId, order.id));
        LogTestCase("UTCID02", "Order ton tai nhung thuoc user khac -> fail, khong goi PayOS.", new { UserId = anotherUserId, CoinOrderId = order.id }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Single(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID02 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID03_SyncMyPayOSOrder_Result_WhenOrderIsNotPayOSOrder()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentGateway: "STRIPE", paymentLinkId: "stripe_001");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.SyncMyPayOSOrderAsync(userId, order.id));
        LogTestCase("UTCID03", "Order khong phai gateway PAYOS -> fail, khong goi PayOS.", new { UserId = userId, CoinOrderId = order.id }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Single(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID03 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID04_SyncMyPayOSOrder_Result_WhenPaymentLinkIdMissing()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentLinkId: null);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.SyncMyPayOSOrderAsync(userId, order.id));
        LogTestCase("UTCID04", "Order PAYOS thieu paymentLinkId -> fail, khong goi PayOS.", new { UserId = userId, CoinOrderId = order.id }, null, ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Single(scope.Store.CoinOrders);
        scope.PayOsMock.VerifyNoOtherCalls();
        LogStore("UTCID04 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID05_SyncMyPayOSOrder_Result_WhenOrderAlreadyPaid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PAID", coinsGranted: 180, paymentLinkId: "plink_already_paid");
        var wallet = CoinPaymentTestHelpers.CreateWallet(userId, balanceCoin: 50);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order, wallet);
        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_already_paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayosStatus("plink_already_paid", "PAID", "00"));

        // Act
        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);
        LogTestCase("UTCID05", "Order da PAID -> tra DTO hien tai, khong cong coin lan nua.", new { UserId = userId, CoinOrderId = order.id }, result);

        // Assert
        Assert.Equal("PAID", result.Status);
        Assert.Equal(50, scope.Store.Wallets.Single(x => x.user_id == userId).balance_coin);
        Assert.Single(scope.Store.CoinOrders);
        scope.PayOsMock.Verify(x => x.GetPaymentRequestAsync("plink_already_paid", It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID05 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID06_SyncMyPayOSOrder_Result_WhenPayOSPaid()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, coinsGranted: 180, paymentLinkId: "plink_paid");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);
        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayosStatus("plink_paid", "PAID", "00"));

        // Act
        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);
        LogTestCase("UTCID06", "PayOS status PAID -> set order PAID va cong coin vao wallet.", new { UserId = userId, CoinOrderId = order.id }, result);

        // Assert
        var wallet = Assert.Single(scope.Store.Wallets, x => x.user_id == userId);
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == order.id);
        Assert.Equal("PAID", result.Status);
        Assert.Equal(180, wallet.balance_coin);
        Assert.Equal("00", savedOrder.gateway_response_code);
        Assert.NotNull(savedOrder.completed_at);
        scope.PayOsMock.Verify(x => x.GetPaymentRequestAsync("plink_paid", It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID06 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID07_SyncMyPayOSOrder_Result_WhenPayOSTerminalStatusIsExpired()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentLinkId: "plink_expired");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);
        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayosStatus("plink_expired", "EXPIRED"));

        // Act
        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);
        LogTestCase("UTCID07", "PayOS terminal status EXPIRED -> order status EXPIRED, khong tao wallet.", new { UserId = userId, CoinOrderId = order.id }, result);

        // Assert
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == order.id);
        Assert.Equal("EXPIRED", result.Status);
        Assert.Equal("EXPIRED", savedOrder.status);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.GetPaymentRequestAsync("plink_expired", It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID07 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID08_SyncMyPayOSOrder_Result_WhenPayOSPending()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PENDING", paymentLinkId: "plink_pending");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);
        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayosStatus("plink_pending", "PENDING"));

        // Act
        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);
        LogTestCase("UTCID08", "PayOS status PENDING -> tra PENDING, khong cong coin.", new { UserId = userId, CoinOrderId = order.id }, result);

        // Assert
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == order.id);
        Assert.Equal("PENDING", result.Status);
        Assert.Equal("PENDING", savedOrder.status);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.GetPaymentRequestAsync("plink_pending", It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID08 sync (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID09_SyncMyPayOSOrder_Result_WhenPayOSStatusUnknown()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentLinkId: "plink_weird");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);
        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_weird", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PayosStatus("plink_weird", "REVERSED"));

        // Act
        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);
        LogTestCase("UTCID09", "PayOS status la -> mark order FAILED, khong cong coin.", new { UserId = userId, CoinOrderId = order.id }, result);

        // Assert
        var savedOrder = Assert.Single(scope.Store.CoinOrders, x => x.id == order.id);
        Assert.Equal("FAILED", result.Status);
        Assert.Equal("FAILED", savedOrder.status);
        Assert.Empty(scope.Store.Wallets);
        scope.PayOsMock.Verify(x => x.GetPaymentRequestAsync("plink_weird", It.IsAny<CancellationToken>()), Times.Once);
        LogStore("UTCID09 sync (sau verify)", scope.Store);
    }
}
