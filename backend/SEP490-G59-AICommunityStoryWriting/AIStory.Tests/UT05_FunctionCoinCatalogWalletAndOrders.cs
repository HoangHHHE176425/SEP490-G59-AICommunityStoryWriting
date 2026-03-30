using BusinessObjects.Entities;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT05_FunctionCoinCatalogWalletAndOrders
{
    private readonly ITestOutputHelper _output;

    public UT05_FunctionCoinCatalogWalletAndOrders(ITestOutputHelper output)
    {
        _output = output;
    }

    private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
    {
        _output.WriteLine("");
        _output.WriteLine($"======== {utcId} | UT05 CoinCatalogWalletAndOrders ========");
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
    public async Task UTCID01_GetActivePackages_ReturnsOnlyActivePackages_WithMappedFields()
    {
        LogUtcContext("UTCID01",
            "Happy path: chỉ lấy package active và map field đúng từ entity sang dto.",
            "Precondition: có 3 package, trong đó 2 active và 1 inactive.",
            "Kỳ vọng: trả đúng 2 package active; field dto map đúng, package inactive không xuất hiện.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var cheap = CoinPaymentTestHelpers.CreatePackage(name: "Cheap", priceAmount: 10000m, coinAmount: 100, bonusCoin: 10, isActive: true);
        var inactive = CoinPaymentTestHelpers.CreatePackage(name: "Inactive", priceAmount: 5000m, coinAmount: 50, isActive: false);
        var premium = CoinPaymentTestHelpers.CreatePackage(name: "Premium", priceAmount: 20000m, coinAmount: 250, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, cheap, inactive, premium);

        var result = await scope.Sut.GetActivePackagesAsync();

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, x => x.Id == inactive.id);

        var cheapDto = Assert.Single(result, x => x.Id == cheap.id);
        var premiumDto = Assert.Single(result, x => x.Id == premium.id);

        Assert.Equal("Cheap", cheapDto.Name);
        Assert.Equal(10000m, cheapDto.PriceAmount);
        Assert.Equal("VND", cheapDto.Currency);
        Assert.Equal(100, cheapDto.CoinAmount);
        Assert.Equal(10, cheapDto.BonusCoin);
        Assert.True(cheapDto.IsActive);

        Assert.Equal("Premium", premiumDto.Name);
        Assert.Equal(20000m, premiumDto.PriceAmount);
        Assert.Equal(250, premiumDto.CoinAmount);
        Assert.Equal(0, premiumDto.BonusCoin);
        Assert.True(premiumDto.IsActive);

        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID02_GetActivePackages_ReturnsEmpty_WhenNoActivePackage()
    {
        LogUtcContext("UTCID02",
            "Boundary path: không có package active.",
            "Precondition: tất cả package đều inactive hoặc null is_active.",
            "Kỳ vọng: trả danh sách rỗng.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        CoinPaymentTestHelpers.Seed(
            scope.DbContext,
            CoinPaymentTestHelpers.CreatePackage(name: "Inactive-1", isActive: false),
            new coin_packages
            {
                id = Guid.NewGuid(),
                name = "Inactive-2",
                price_amount = 15000m,
                currency = "VND",
                coin_amount = 120,
                bonus_coin = 0,
                is_active = null
            });

        var result = await scope.Sut.GetActivePackagesAsync();

        Assert.Empty(result);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID03_GetActivePackages_ReturnsPackagesOrderedByPriceAscending()
    {
        LogUtcContext("UTCID03",
            "Happy path: nhiều package active phải được sort tăng dần theo price_amount.",
            "Precondition: có ít nhất 2 package active với price_amount khác nhau.",
            "Kỳ vọng: danh sách trả về được sắp xếp tăng dần theo giá.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var expensive = CoinPaymentTestHelpers.CreatePackage(name: "Expensive", priceAmount: 30000m, coinAmount: 300, isActive: true);
        var cheapest = CoinPaymentTestHelpers.CreatePackage(name: "Cheapest", priceAmount: 9000m, coinAmount: 90, isActive: true);
        var middle = CoinPaymentTestHelpers.CreatePackage(name: "Middle", priceAmount: 15000m, coinAmount: 150, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, expensive, cheapest, middle);

        var result = await scope.Sut.GetActivePackagesAsync();

        Assert.Equal(3, result.Count);
        Assert.Equal(cheapest.id, result[0].Id);
        Assert.Equal(middle.id, result[1].Id);
        Assert.Equal(expensive.id, result[2].Id);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID01_GetOrCreateWallet_ReturnsExistingWallet_WithoutCreatingNewRow()
    {
        LogUtcContext("UTCID01",
            "Happy path: user đã có wallet thì trả về wallet hiện có.",
            "Precondition: user và wallet tồn tại sẵn trong DB.",
            "Kỳ vọng: không tạo row mới, số dư giữ nguyên.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "wallet-owner@example.com");
        var wallet = CoinPaymentTestHelpers.CreateWallet(user.id, balanceCoin: 321, incomeBalance: 45m, frozenBalance: 6m);
        CoinPaymentTestHelpers.Seed(scope.DbContext, user, wallet);

        var result = await scope.Sut.GetOrCreateWalletAsync(user.id);

        Assert.Equal(user.id, result.UserId);
        Assert.Equal(321, result.BalanceCoin);
        Assert.Equal(45m, result.IncomeBalance);
        Assert.Equal(1, scope.DbContext.wallets.Count());
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID02_GetOrCreateWallet_CreatesWallet_WhenMissing()
    {
        LogUtcContext("UTCID02",
            "Abnormal path: user chưa có wallet thì service tự tạo wallet mặc định.",
            "Precondition: user tồn tại nhưng chưa có row wallet.",
            "Kỳ vọng: tạo wallet mới balance 0, currency VND.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "new-wallet@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, user);

        var result = await scope.Sut.GetOrCreateWalletAsync(user.id);

        var savedWallet = scope.DbContext.wallets.Single(x => x.user_id == user.id);
        Assert.Equal(0, result.BalanceCoin);
        Assert.Equal("VND", result.Currency);
        Assert.Equal(0, savedWallet.balance_coin);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID03_GetOrCreateWallet_CurrentlyThrowsEfError_WhenUserIdIsGuidEmpty()
    {
        LogUtcContext("UTCID03",
            "Boundary path theo report: userId = Guid.Empty.",
            "Excel kỳ vọng: throw 'UserId cannot be empty!'.",
            "Current service chưa có validation này; hành vi thực tế hiện tại là EF ném InvalidOperationException khi SaveChanges.");

        using var scope = CoinPaymentTestHelpers.CreateScope();

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.Sut.GetOrCreateWalletAsync(Guid.Empty));

        Assert.Contains("wallets.user_id", ex.Message, StringComparison.Ordinal);
        LogActualMessage(ex.Message);
    }

    [Fact]
    public async Task UTCID01_GetMyOrders_ReturnsOnlyCurrentUsersOrders_InDescendingCreatedAt_WithTake10()
    {
        LogUtcContext("UTCID01",
            "Happy path: chỉ lấy order của đúng user, sort mới nhất trước, tối đa 10 phần tử.",
            "Precondition: coin_orders có dữ liệu của user A và user B.",
            "Input: userId = A, take = 10.",
            "Kỳ vọng: chỉ trả order của A, sort theo created_at giảm dần, số phần tử không vượt quá 10.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "buyer@example.com");
        var anotherUser = CoinPaymentTestHelpers.CreateUser(email: "other@example.com");
        var package = CoinPaymentTestHelpers.CreatePackage();

        var userOrders = Enumerable.Range(0, 12)
            .Select(i => CoinPaymentTestHelpers.CreateOrder(
                user.id,
                package.id,
                paymentLinkId: $"plink-a-{i}",
                createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();

        var foreignOrder = CoinPaymentTestHelpers.CreateOrder(anotherUser.id, package.id, paymentLinkId: "plink-foreign", createdAt: DateTime.UtcNow);
        CoinPaymentTestHelpers.Seed(scope.DbContext, user, anotherUser, package);
        CoinPaymentTestHelpers.Seed(scope.DbContext, userOrders.Cast<object>().Concat(new object[] { foreignOrder }).ToArray());

        var result = await scope.Sut.GetMyOrdersAsync(user.id, take: 10);

        Assert.Equal(10, result.Count);
        Assert.Equal(userOrders[0].id, result[0].Id);
        Assert.Equal(userOrders[9].id, result[9].Id);
        Assert.DoesNotContain(result, x => x.Id == foreignOrder.id);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID02_GetMyOrders_ReturnsEmpty_WhenUserHasNoOrders()
    {
        LogUtcContext("UTCID02",
            "Normal path: user không có coin order nào.",
            "Precondition: DB không có coin_orders thuộc userId.",
            "Input: userId bất kỳ, take = 50.",
            "Kỳ vọng: trả danh sách rỗng.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "empty-orders@example.com");
        var otherUser = CoinPaymentTestHelpers.CreateUser(email: "other-orders@example.com");
        var package = CoinPaymentTestHelpers.CreatePackage();
        var foreignOrder = CoinPaymentTestHelpers.CreateOrder(otherUser.id, package.id, paymentLinkId: "plink-foreign-only");
        CoinPaymentTestHelpers.Seed(scope.DbContext, user, otherUser, package, foreignOrder);

        var result = await scope.Sut.GetMyOrdersAsync(user.id, take: 50);

        Assert.Empty(result);
        LogActualReturn(result);
    }

    [Fact]
    public async Task UTCID03_GetMyOrders_ClampsTake_ToMinimum1_AndMaximum200()
    {
        LogUtcContext("UTCID03",
            "Boundary path: take < 1 hoặc take > 200 phải bị clamp.",
            "Precondition: có đủ dữ liệu order cho cùng một user.",
            "Input: take = 0 và take = 500.",
            "Kỳ vọng: take thực tế lần lượt là 1 và 200.");

        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "clamp-orders@example.com");
        var package = CoinPaymentTestHelpers.CreatePackage();
        var orders = Enumerable.Range(0, 205)
            .Select(i => CoinPaymentTestHelpers.CreateOrder(
                user.id,
                package.id,
                paymentLinkId: $"plink-clamp-{i}",
                createdAt: DateTime.UtcNow.AddMinutes(-i)))
            .ToArray();

        CoinPaymentTestHelpers.Seed(scope.DbContext, user, package);
        CoinPaymentTestHelpers.Seed(scope.DbContext, orders);

        var resultMin = await scope.Sut.GetMyOrdersAsync(user.id, take: 0);
        var resultMax = await scope.Sut.GetMyOrdersAsync(user.id, take: 500);

        Assert.Single(resultMin);
        Assert.Equal(200, resultMax.Count);
        Assert.Equal(orders[0].id, resultMin[0].Id);
        Assert.Equal(orders[0].id, resultMax[0].Id);
        Assert.Equal(orders[199].id, resultMax[199].Id);
        LogActualReturn(new
        {
            Take0 = resultMin,
            Take500Count = resultMax.Count,
            Take500FirstId = resultMax[0].Id,
            Take500LastId = resultMax[199].Id
        });
    }
}
