using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;

namespace AIStory.Tests;

internal sealed class CoinPaymentTestScope : IDisposable
{
    public StoryPlatformDbContext DbContext { get; }
    public Mock<IPayOSClient> PayOsMock { get; }
    public Mock<INotificationHubNotifier> NotificationHubNotifierMock { get; }
    public IConfiguration Configuration { get; }
    public CoinPaymentService Sut { get; }

    public CoinPaymentTestScope(
        StoryPlatformDbContext dbContext,
        Mock<IPayOSClient> payOsMock,
        Mock<INotificationHubNotifier> notificationHubNotifierMock,
        IConfiguration configuration)
    {
        DbContext = dbContext;
        PayOsMock = payOsMock;
        NotificationHubNotifierMock = notificationHubNotifierMock;
        Configuration = configuration;
        Sut = new CoinPaymentService(
            dbContext,
            payOsMock.Object,
            configuration,
            NullLogger<CoinPaymentService>.Instance,
            notificationHubNotifierMock.Object);
    }

    public void Dispose()
    {
        DbContext.Database.EnsureDeleted();
        DbContext.Dispose();
    }
}

internal static class CoinPaymentTestHelpers
{
    public static CoinPaymentTestScope CreateScope(
        IDictionary<string, string?>? configurationValues = null,
        MockBehavior mockBehavior = MockBehavior.Strict)
    {
        var options = new DbContextOptionsBuilder<StoryPlatformDbContext>()
            .UseInMemoryDatabase($"coin-payment-tests-{Guid.NewGuid():N}")
            .ConfigureWarnings(x => x.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PayOS:DefaultExpiredMinutes"] = "15",
                ["PayOS:ChecksumKey"] = "unit-test-checksum"
            })
            .AddInMemoryCollection(configurationValues)
            .Build();

        return new CoinPaymentTestScope(
            new StoryPlatformDbContext(options),
            new Mock<IPayOSClient>(mockBehavior),
            new Mock<INotificationHubNotifier>(mockBehavior),
            configuration);
    }

    public static users CreateUser(
        Guid? id = null,
        string email = "user@example.com",
        string status = "ACTIVE",
        string? nickname = null,
        DateTime? createdAt = null)
    {
        var userId = id ?? Guid.NewGuid();
        var user = new users
        {
            id = userId,
            email = email,
            password_hash = "hashed-password",
            role = "USER",
            status = status,
            created_at = createdAt ?? DateTime.UtcNow.AddDays(-30),
            updated_at = createdAt ?? DateTime.UtcNow.AddDays(-1)
        };

        if (!string.IsNullOrWhiteSpace(nickname))
        {
            user.user_profiles = new user_profiles
            {
                user_id = userId,
                nickname = nickname,
                updated_at = DateTime.UtcNow
            };
        }

        return user;
    }

    public static coin_packages CreatePackage(
        Guid? id = null,
        string name = "Starter Pack",
        decimal priceAmount = 10000m,
        int coinAmount = 100,
        int bonusCoin = 0,
        bool isActive = true)
    {
        return new coin_packages
        {
            id = id ?? Guid.NewGuid(),
            name = name,
            price_amount = priceAmount,
            currency = "VND",
            coin_amount = coinAmount,
            bonus_coin = bonusCoin,
            is_active = isActive,
            created_at = DateTime.UtcNow
        };
    }

    public static wallets CreateWallet(
        Guid userId,
        int balanceCoin = 0,
        decimal incomeBalance = 0m,
        decimal frozenBalance = 0m,
        decimal pendingEscrowBalance = 0m)
    {
        return new wallets
        {
            user_id = userId,
            balance_coin = balanceCoin,
            income_balance = incomeBalance,
            frozen_balance = frozenBalance,
            pending_escrow_balance = pendingEscrowBalance,
            currency = "VND",
            updated_at = DateTime.UtcNow
        };
    }

    public static coin_orders CreateOrder(
        Guid userId,
        Guid packageId,
        decimal amountPaid = 10000m,
        int coinsGranted = 100,
        string status = "PENDING",
        string paymentGateway = "PAYOS",
        string? paymentLinkId = "plink_001",
        DateTime? createdAt = null)
    {
        return new coin_orders
        {
            id = Guid.NewGuid(),
            user_id = userId,
            package_id = packageId,
            amount_paid = amountPaid,
            coins_granted = coinsGranted,
            payment_gateway = paymentGateway,
            status = status,
            gateway_transaction_id = paymentLinkId,
            created_at = createdAt ?? DateTime.UtcNow
        };
    }

    public static platform_wallet CreatePlatformWallet(int balanceCoin = 0)
    {
        return new platform_wallet
        {
            id = 1,
            balance_coin = balanceCoin,
            updated_at = DateTime.UtcNow
        };
    }

    public static string BuildWebhookBody(string paymentLinkId, string code)
    {
        return JsonSerializer.Serialize(new
        {
            signature = "__SIGNATURE__",
            data = new
            {
                paymentLinkId,
                code
            }
        });
    }

    public static void Seed(StoryPlatformDbContext dbContext, params object[] entities)
    {
        dbContext.AddRange(entities);
        dbContext.SaveChanges();
    }
}
