using Services.DTOs.Payments;
using Services.Integrations.PayOS;
using Moq;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT06_FunctionPayOSTopup
{
    private readonly ITestOutputHelper _output;

    public UT06_FunctionPayOSTopup(ITestOutputHelper output)
    {
        _output = output;
    }

    private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
    {
        _output.WriteLine("");
        _output.WriteLine($"======== {utcId} | UT06 PayOSTopup ========");
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
    public async Task UTCID01_CreatePayOSPayment_Fails_WhenPackageNotFound()
    {
        LogUtcContext("UTCID01",
            "Abnormal path: package không tồn tại.",
            "Precondition: request.PackageId không có trong DB.",
            "Kỳ vọng: throw InvalidOperationException Coin package not found.");

        using var scope = CoinPaymentTestHelpers.CreateScope();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.CreatePayOSPaymentAsync(Guid.NewGuid(), new CreatePayOSPaymentRequestDto
        {
            PackageId = Guid.NewGuid(),
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        }));

        Assert.Equal("Coin package not found.", ex.Message);
        LogActualMessage(ex.Message);
        scope.PayOsMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID02_CreatePayOSPayment_Fails_WhenPackageInactive()
    {
        LogUtcContext("UTCID02",
            "Abnormal path: package tồn tại nhưng inactive.",
            "Precondition: package is_active = false.",
            "Kỳ vọng: throw InvalidOperationException Coin package is not active.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: false);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.CreatePayOSPaymentAsync(Guid.NewGuid(), new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        }));

        Assert.Equal("Coin package is not active.", ex.Message);
        LogActualMessage(ex.Message);
        scope.PayOsMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task UTCID03_CreatePayOSPayment_Fails_WhenPackageCoinConfigurationInvalid()
    {
        LogUtcContext("UTCID03",
            "Abnormal path: package có tổng coin <= 0.",
            "Precondition: coin_amount + bonus_coin = 0.",
            "Kỳ vọng: throw InvalidOperationException Invalid coin package configuration.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var package = CoinPaymentTestHelpers.CreatePackage(coinAmount: 0, bonusCoin: 0);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.CreatePayOSPaymentAsync(Guid.NewGuid(), new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        }));

        Assert.Equal("Invalid coin package configuration.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID04_CreatePayOSPayment_Fails_WhenPackagePriceInvalid()
    {
        LogUtcContext("UTCID04",
            "Abnormal path: package có price_amount <= 0.",
            "Precondition: package active nhưng giá = 0.",
            "Kỳ vọng: throw InvalidOperationException Invalid coin package price.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var package = CoinPaymentTestHelpers.CreatePackage(priceAmount: 0m, coinAmount: 100, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.CreatePayOSPaymentAsync(Guid.NewGuid(), new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        }));

        Assert.Equal("Invalid coin package price.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID05_CreatePayOSPayment_Succeeds_WhenPackageValid()
    {
        LogUtcContext("UTCID05",
            "Happy path: package hợp lệ thì tạo coin order PENDING và trả checkout url.",
            "Precondition: package active, coin_amount và price_amount hợp lệ.",
            "Kỳ vọng: lưu coin order, lưu gateway_transaction_id và trả CreatePayOSPaymentResponseDto.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(priceAmount: 12000m, coinAmount: 120, bonusCoin: 30, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);

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

        var result = await scope.Sut.CreatePayOSPaymentAsync(userId, new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        });

        var savedOrder = scope.DbContext.coin_orders.Single(x => x.id == result.CoinOrderId);
        Assert.Equal(package.id, result.PackageId);
        Assert.Equal(150, result.CoinsGranted);
        Assert.Equal("PENDING", savedOrder.status);
        Assert.Equal("plink_123", savedOrder.gateway_transaction_id);
        Assert.Equal("00", savedOrder.gateway_response_code);
        Assert.True(result.OrderCode > 0);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID06_CreatePayOSPayment_SendsExpiredAt_WithinValidRange()
    {
        LogUtcContext("UTCID06",
            "Boundary path: expiredAt gửi sang PayOS phải nằm trong range hợp lệ.",
            "Precondition: cấu hình PayOS DefaultExpiredMinutes được áp dụng hoặc clamp về range cho phép.",
            "Kỳ vọng: expiredAt được truyền cho PayOS trong khoảng 1 phút đến 7 ngày.");

        using var scope = CoinPaymentTestHelpers.CreateScope(new Dictionary<string, string?>
        {
            ["PayOS:DefaultExpiredMinutes"] = "0"
        });

        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);

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
            .Callback<long, decimal, string, string, string, int?, CancellationToken>((_, _, _, _, _, expiredAt, _) =>
            {
                capturedExpiredAt = expiredAt;
            })
            .ReturnsAsync(new PayOSClient.CreatePaymentLinkResult("plink_exp", "https://payos.test/checkout/exp", "{\"code\":\"00\"}", "00"));

        var beforeMin = DateTimeOffset.UtcNow.AddMinutes(1).ToUnixTimeSeconds();
        var result = await scope.Sut.CreatePayOSPaymentAsync(userId, new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://test.local/cancel",
            ReturnUrl = "https://test.local/return"
        });
        var afterMax = DateTimeOffset.UtcNow.AddDays(7).ToUnixTimeSeconds();

        Assert.NotNull(capturedExpiredAt);
        Assert.InRange(capturedExpiredAt.Value, (int)beforeMin - 5, (int)afterMax + 5);
        LogActualReturn(new
        {
            result,
            expiredAt = capturedExpiredAt
        });
    }

    [Fact]
    public async Task UTCID07_CreatePayOSPayment_PersistsFailedOrder_WhenPayOSThrows()
    {
        LogUtcContext("UTCID07",
            "Abnormal path: PayOS CreatePaymentLinkAsync ném exception.",
            "Precondition: package hợp lệ, mock PayOS throw.",
            "Kỳ vọng: throw ra ngoài; DB có đúng 1 coin_order FAILED + gateway_response_code ghi lỗi; không có PENDING mồ côi.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);

        scope.PayOsMock
            .Setup(x => x.CreatePaymentLinkAsync(
                It.IsAny<long>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("PayOS error 503: maintenance"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.CreatePayOSPaymentAsync(userId, new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        }));

        Assert.Contains("503", ex.Message);
        var failed = scope.DbContext.coin_orders.Single();
        Assert.Equal("FAILED", failed.status);
        Assert.Null(failed.gateway_transaction_id);
        Assert.NotNull(failed.gateway_response_code);
        Assert.Contains("503", failed.gateway_response_code!);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID08_CreatePayOSPayment_PersistsFailedOrder_WhenPayOSCodeRejected()
    {
        LogUtcContext("UTCID08",
            "Abnormal path: PayOS trả HTTP 200 nhưng code != 00.",
            "Precondition: mock trả CreatePaymentLinkResult với Code = 99.",
            "Kỳ vọng: throw; DB có coin_order FAILED.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage(isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package);

        scope.PayOsMock
            .Setup(x => x.CreatePaymentLinkAsync(
                It.IsAny<long>(),
                It.IsAny<decimal>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<int?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.CreatePaymentLinkResult("plink_x", "https://payos.test/checkout/x", "{}", "99"));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.CreatePayOSPaymentAsync(userId, new CreatePayOSPaymentRequestDto
        {
            PackageId = package.id,
            CancelUrl = "https://app.test/cancel",
            ReturnUrl = "https://app.test/return"
        }));

        Assert.Contains("99", ex.Message);
        var failed = scope.DbContext.coin_orders.Single();
        Assert.Equal("FAILED", failed.status);
        Assert.Null(failed.gateway_transaction_id);
        Assert.Contains("99", failed.gateway_response_code ?? string.Empty);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID01_SyncMyPayOSOrder_Fails_WhenOrderNotFound()
    {
        LogUtcContext("UTCID01",
            "Abnormal path: coinOrderId không tồn tại trong DB.",
            "Precondition: userId hợp lệ nhưng DB không có order tương ứng.",
            "Kỳ vọng: throw InvalidOperationException Order not found.");

        using var scope = CoinPaymentTestHelpers.CreateScope();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.SyncMyPayOSOrderAsync(Guid.NewGuid(), Guid.NewGuid()));

        Assert.Equal("Order not found.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID02_SyncMyPayOSOrder_Fails_WhenOrderBelongsToDifferentUser()
    {
        LogUtcContext("UTCID02",
            "Abnormal path: coinOrderId đúng nhưng thuộc user khác.",
            "Precondition: order tồn tại nhưng userId truyền vào không phải chủ sở hữu.",
            "Kỳ vọng: throw InvalidOperationException Order not found.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var ownerId = Guid.NewGuid();
        var anotherUserId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(ownerId, package.id, paymentLinkId: "plink-owner");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.SyncMyPayOSOrderAsync(anotherUserId, order.id));

        Assert.Equal("Order not found.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID03_SyncMyPayOSOrder_Fails_WhenOrderIsNotPayOSOrder()
    {
        LogUtcContext("UTCID03",
            "Abnormal path: order tồn tại nhưng payment gateway khác PAYOS.",
            "Precondition: order thuộc user hiện tại nhưng payment_gateway != PAYOS.",
            "Kỳ vọng: throw InvalidOperationException Order is not a PayOS order.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentGateway: "STRIPE", paymentLinkId: "stripe_001");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.SyncMyPayOSOrderAsync(userId, order.id));

        Assert.Equal("Order is not a PayOS order.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID04_SyncMyPayOSOrder_Fails_WhenPaymentLinkIdMissing()
    {
        LogUtcContext("UTCID04",
            "Abnormal path: order có gateway PAYOS nhưng thiếu paymentLinkId.",
            "Precondition: gateway_transaction_id null hoặc empty.",
            "Kỳ vọng: throw InvalidOperationException Missing paymentLinkId for this order.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentLinkId: null);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.Sut.SyncMyPayOSOrderAsync(userId, order.id));

        Assert.Equal("Missing paymentLinkId for this order.", ex.Message);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID05_SyncMyPayOSOrder_ReturnsCurrentOrder_WhenOrderAlreadyPaid()
    {
        LogUtcContext("UTCID05",
            "Normal path: order đã PAID trước đó.",
            "Precondition: order.status = PAID.",
            "Kỳ vọng: trả CoinOrderDto hiện tại, không cộng thêm coin vào wallet.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PAID", coinsGranted: 180, paymentLinkId: "plink_already_paid");
        var wallet = CoinPaymentTestHelpers.CreateWallet(userId, balanceCoin: 50);
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order, wallet);

        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_already_paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.GetPaymentRequestResult(
                "plink_already_paid",
                "PAID",
                1,
                10000,
                10000,
                0,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                null,
                "{\"status\":\"PAID\"}",
                "00"));

        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);

        Assert.Equal("PAID", result.Status);
        Assert.Equal(50, scope.DbContext.wallets.Single(x => x.user_id == userId).balance_coin);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID06_SyncMyPayOSOrder_MarksPaid_AndCreditsWallet_WhenPayOSPaid()
    {
        LogUtcContext("UTCID06",
            "Normal path: GetPaymentRequestAsync trả Status = PAID.",
            "Precondition: order chưa PAID, paymentLinkId hợp lệ.",
            "Kỳ vọng: cộng coins, set order = PAID, completed_at được gán.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, coinsGranted: 180, paymentLinkId: "plink_paid");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_paid", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.GetPaymentRequestResult(
                "plink_paid",
                "PAID",
                1,
                10000,
                10000,
                0,
                DateTimeOffset.UtcNow.AddMinutes(-5),
                null,
                "{\"status\":\"PAID\"}",
                "00"));

        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);

        var wallet = scope.DbContext.wallets.Single(x => x.user_id == userId);
        var savedOrder = scope.DbContext.coin_orders.Single(x => x.id == order.id);
        Assert.Equal("PAID", result.Status);
        Assert.Equal(180, wallet.balance_coin);
        Assert.Equal("00", savedOrder.gateway_response_code);
        Assert.NotNull(savedOrder.completed_at);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID07_SyncMyPayOSOrder_ReturnsMatchingTerminalStatus_WhenPayOSCancelledOrExpired()
    {
        LogUtcContext("UTCID07",
            "Normal path: GetPaymentRequestAsync trả terminal status tương ứng.",
            "Precondition: order PENDING, paymentLinkId hợp lệ.",
            "Kỳ vọng: order.status nhận đúng CANCELLED hoặc EXPIRED.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentLinkId: "plink_expired");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_expired", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.GetPaymentRequestResult(
                "plink_expired",
                "EXPIRED",
                2,
                10000,
                0,
                10000,
                DateTimeOffset.UtcNow.AddMinutes(-10),
                DateTimeOffset.UtcNow.AddMinutes(-1),
                "{\"status\":\"EXPIRED\"}",
                null));

        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);

        Assert.Equal("EXPIRED", result.Status);
        Assert.Empty(scope.DbContext.wallets);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID08_SyncMyPayOSOrder_ReturnsPendingWithoutCreditingCoins()
    {
        LogUtcContext("UTCID08",
            "Normal path: GetPaymentRequestAsync trả Status = PENDING.",
            "Precondition: order chưa PAID, paymentLinkId hợp lệ.",
            "Kỳ vọng: trả CoinOrderDto với trạng thái chưa thanh toán, không cộng coin.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, status: "PENDING", paymentLinkId: "plink_pending");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_pending", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.GetPaymentRequestResult(
                "plink_pending",
                "PENDING",
                3,
                10000,
                0,
                10000,
                DateTimeOffset.UtcNow.AddMinutes(-10),
                null,
                "{\"status\":\"PENDING\"}",
                null));

        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);

        Assert.Equal("PENDING", result.Status);
        Assert.Empty(scope.DbContext.wallets);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID09_SyncMyPayOSOrder_MarksFailed_WhenPayOSStatusUnknown()
    {
        LogUtcContext("UTCID09",
            "Abnormal path: PayOS trả trạng thái terminal lạ.",
            "Precondition: order PENDING, paymentLinkId hợp lệ.",
            "Kỳ vọng: order bị mark FAILED.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.NewGuid();
        var package = CoinPaymentTestHelpers.CreatePackage();
        var order = CoinPaymentTestHelpers.CreateOrder(userId, package.id, paymentLinkId: "plink_weird");
        CoinPaymentTestHelpers.Seed(scope.DbContext, package, order);

        scope.PayOsMock
            .Setup(x => x.GetPaymentRequestAsync("plink_weird", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PayOSClient.GetPaymentRequestResult(
                "plink_weird",
                "REVERSED",
                4,
                10000,
                0,
                10000,
                DateTimeOffset.UtcNow.AddMinutes(-10),
                null,
                "{\"status\":\"REVERSED\"}",
                null));

        var result = await scope.Sut.SyncMyPayOSOrderAsync(userId, order.id);

        Assert.Equal("FAILED", result.Status);
        Assert.Empty(scope.DbContext.wallets);
        LogActualReturn(result);
    }
}
