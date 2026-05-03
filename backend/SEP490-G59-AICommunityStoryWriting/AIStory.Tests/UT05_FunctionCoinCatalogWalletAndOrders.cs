using BusinessObjects.Entities;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT05_FunctionCoinCatalogWalletAndOrders
{
    private readonly ITestOutputHelper _output;

    public UT05_FunctionCoinCatalogWalletAndOrders(ITestOutputHelper output)
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
        _output.WriteLine($"Packages={store.CoinPackages.Count}, Wallets={store.Wallets.Count}, Orders={store.CoinOrders.Count}, Users={store.Users.Count}");
    }

    [Fact]
    public async Task UTCID01_GetActivePackages_Result_WhenActivePackagesExist()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var cheap = CoinPaymentTestHelpers.CreatePackage(name: "Cheap", priceAmount: 10000m, coinAmount: 100, bonusCoin: 10, isActive: true);
        var inactive = CoinPaymentTestHelpers.CreatePackage(name: "Inactive", priceAmount: 5000m, coinAmount: 50, isActive: false);
        var premium = CoinPaymentTestHelpers.CreatePackage(name: "Premium", priceAmount: 20000m, coinAmount: 250, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, cheap, inactive, premium);

        // Act
        var result = await scope.Sut.GetActivePackagesAsync();
        LogTestCase(
            "UTCID01",
            "Chi lay package active va map field dung tu entity sang DTO.",
            new { SeededPackages = scope.Store.CoinPackages.Select(p => new { p.id, p.name, p.price_amount, p.is_active }) },
            result);

        // Assert
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
        Assert.Equal(3, scope.Store.CoinPackages.Count);
        LogStore("UTCID01 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID02_GetActivePackages_Result_WhenNoActivePackage()
    {
        // Arrange
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

        // Act
        var result = await scope.Sut.GetActivePackagesAsync();
        LogTestCase(
            "UTCID02",
            "Khong co package active -> tra danh sach rong.",
            new { SeededPackages = scope.Store.CoinPackages.Select(p => new { p.id, p.name, p.is_active }) },
            result);

        // Assert
        Assert.Empty(result);
        Assert.Equal(2, scope.Store.CoinPackages.Count);
        LogStore("UTCID02 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID03_GetActivePackages_Result_WhenOrderingByPriceAscending()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var expensive = CoinPaymentTestHelpers.CreatePackage(name: "Expensive", priceAmount: 30000m, coinAmount: 300, isActive: true);
        var cheapest = CoinPaymentTestHelpers.CreatePackage(name: "Cheapest", priceAmount: 9000m, coinAmount: 90, isActive: true);
        var middle = CoinPaymentTestHelpers.CreatePackage(name: "Middle", priceAmount: 15000m, coinAmount: 150, isActive: true);
        CoinPaymentTestHelpers.Seed(scope.DbContext, expensive, cheapest, middle);

        // Act
        var result = await scope.Sut.GetActivePackagesAsync();
        LogTestCase(
            "UTCID03",
            "Nhieu package active -> sort tang dan theo price_amount.",
            new { SeededPackages = scope.Store.CoinPackages.Select(p => new { p.id, p.name, p.price_amount }) },
            result);

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(cheapest.id, result[0].Id);
        Assert.Equal(middle.id, result[1].Id);
        Assert.Equal(expensive.id, result[2].Id);
        LogStore("UTCID03 (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID01_GetOrCreateWallet_Result_WhenWalletAlreadyExists()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "wallet-owner@example.com");
        var wallet = CoinPaymentTestHelpers.CreateWallet(user.id, balanceCoin: 321, incomeBalance: 45m, frozenBalance: 6m);
        CoinPaymentTestHelpers.Seed(scope.DbContext, user, wallet);

        // Act
        var result = await scope.Sut.GetOrCreateWalletAsync(user.id);
        LogTestCase(
            "UTCID01",
            "User da co wallet -> tra wallet hien co, khong tao row moi.",
            new { UserId = user.id, ExistingWallet = new { wallet.balance_coin, wallet.income_balance, wallet.frozen_balance } },
            result);

        // Assert
        Assert.Equal(user.id, result.UserId);
        Assert.Equal(321, result.BalanceCoin);
        Assert.Equal(45m, result.IncomeBalance);
        Assert.Single(scope.Store.Wallets);
        Assert.Equal(321, scope.Store.Wallets.Single().balance_coin);
        LogStore("UTCID01 wallet (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID02_GetOrCreateWallet_Result_WhenWalletMissing()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "new-wallet@example.com");
        CoinPaymentTestHelpers.Seed(scope.DbContext, user);

        // Act
        var result = await scope.Sut.GetOrCreateWalletAsync(user.id);
        LogTestCase(
            "UTCID02",
            "User chua co wallet -> service tao wallet mac dinh.",
            new { UserId = user.id },
            result);

        // Assert
        var savedWallet = Assert.Single(scope.Store.Wallets, x => x.user_id == user.id);
        Assert.Equal(0, result.BalanceCoin);
        Assert.Equal("VND", result.Currency);
        Assert.Equal(0, savedWallet.balance_coin);
        LogStore("UTCID02 wallet (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID03_GetOrCreateWallet_Result_WhenUserIdIsGuidEmpty()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var userId = Guid.Empty;

        // Act
        var ex = await Record.ExceptionAsync(() => scope.Sut.GetOrCreateWalletAsync(userId));
        LogTestCase(
            "UTCID03",
            "UserId Guid.Empty -> service hien tai fail khi SaveChanges, khong persist wallet vao store.",
            new { UserId = userId },
            null,
            ex);

        // Assert
        Assert.NotNull(ex);
        Assert.Empty(scope.Store.Wallets);
        LogStore("UTCID03 wallet (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID01_GetMyOrders_Result_WhenUserHasManyOrders()
    {
        // Arrange
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

        // Act
        var result = await scope.Sut.GetMyOrdersAsync(user.id, take: 10);
        LogTestCase(
            "UTCID01",
            "Chi lay order cua user hien tai, sort moi nhat truoc va gioi han take=10.",
            new { UserId = user.id, Take = 10, TotalStoreOrders = scope.Store.CoinOrders.Count },
            result);

        // Assert
        Assert.Equal(10, result.Count);
        Assert.Equal(userOrders[0].id, result[0].Id);
        Assert.Equal(userOrders[9].id, result[9].Id);
        Assert.DoesNotContain(result, x => x.Id == foreignOrder.id);
        Assert.Equal(13, scope.Store.CoinOrders.Count);
        LogStore("UTCID01 orders (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID02_GetMyOrders_Result_WhenUserHasNoOrders()
    {
        // Arrange
        using var scope = CoinPaymentTestHelpers.CreateScope();
        var user = CoinPaymentTestHelpers.CreateUser(email: "empty-orders@example.com");
        var otherUser = CoinPaymentTestHelpers.CreateUser(email: "other-orders@example.com");
        var package = CoinPaymentTestHelpers.CreatePackage();
        var foreignOrder = CoinPaymentTestHelpers.CreateOrder(otherUser.id, package.id, paymentLinkId: "plink-foreign-only");
        CoinPaymentTestHelpers.Seed(scope.DbContext, user, otherUser, package, foreignOrder);

        // Act
        var result = await scope.Sut.GetMyOrdersAsync(user.id, take: 50);
        LogTestCase(
            "UTCID02",
            "User khong co coin order -> tra danh sach rong.",
            new { UserId = user.id, Take = 50, TotalStoreOrders = scope.Store.CoinOrders.Count },
            result);

        // Assert
        Assert.Empty(result);
        Assert.Single(scope.Store.CoinOrders);
        LogStore("UTCID02 orders (sau verify)", scope.Store);
    }

    [Fact]
    public async Task UTCID03_GetMyOrders_Result_WhenTakeOutsideAllowedRange()
    {
        // Arrange
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

        // Act
        var resultMin = await scope.Sut.GetMyOrdersAsync(user.id, take: 0);
        var resultMax = await scope.Sut.GetMyOrdersAsync(user.id, take: 500);
        var output = new
        {
            Take0 = resultMin,
            Take500Count = resultMax.Count,
            Take500FirstId = resultMax[0].Id,
            Take500LastId = resultMax[199].Id
        };
        LogTestCase(
            "UTCID03",
            "take < 1 va take > 200 duoc clamp lan luot ve 1 va 200.",
            new { UserId = user.id, Takes = new[] { 0, 500 }, TotalStoreOrders = scope.Store.CoinOrders.Count },
            output);

        // Assert
        Assert.Single(resultMin);
        Assert.Equal(200, resultMax.Count);
        Assert.Equal(orders[0].id, resultMin[0].Id);
        Assert.Equal(orders[0].id, resultMax[0].Id);
        Assert.Equal(orders[199].id, resultMax[199].Id);
        Assert.Equal(205, scope.Store.CoinOrders.Count);
        LogStore("UTCID03 orders (sau verify)", scope.Store);
    }
}
