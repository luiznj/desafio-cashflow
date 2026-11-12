using CashFlow.Transaction.Db.Data;
using CashFlow.Transaction.Db.Models;
using CashFlow.Transaction.Db.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;

namespace CashFlow.Transaction.Api.IntegrationTests;

public class TransactionRepository_IntegrationTests : IClassFixture<TransactionApiFactory>
{
    private readonly TransactionApiFactory _factory;

    public TransactionRepository_IntegrationTests(TransactionApiFactory factory)
    {
        // Force host creation so migrations run before tests
        factory.CreateClient().Dispose();
        _factory = factory;
    }

    private IServiceScope CreateScope() => _factory.Services.CreateScope();

    [Fact]
    public async Task AddCreditAsync_StoresPositiveAmountInDb()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        var entry = await repo.AddCreditAsync(userId, 100m, null, key);

        var stored = await db.Transactions.AsNoTracking().FirstAsync(t => t.Id == entry.Id);
        stored.Amount.Should().Be(100m);
        stored.Type.Should().Be(TransactionType.Credit);
    }

    [Fact]
    public async Task AddDebitAsync_StoresNegativeAmountInDb()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();

        var entry = await repo.AddDebitAsync(userId, 50m, null, key);

        var stored = await db.Transactions.AsNoTracking().FirstAsync(t => t.Id == entry.Id);
        stored.Amount.Should().Be(-50m);
        stored.Type.Should().Be(TransactionType.Debit);
    }

    [Fact]
    public async Task FindByIdempotencyKeyAsync_ExistingKey_ReturnsTransaction()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var key = Guid.NewGuid().ToString();
        await repo.AddCreditAsync(Guid.NewGuid(), 100m, null, key);

        var result = await repo.FindByIdempotencyKeyAsync(key);

        result.Should().NotBeNull();
        result!.IdempotencyKey.Should().Be(key);
    }

    [Fact]
    public async Task FindByIdempotencyKeyAsync_UnknownKey_ReturnsNull()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();

        var result = await repo.FindByIdempotencyKeyAsync(Guid.NewGuid().ToString());

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAsync_FiltersCorrectlyByUserId()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var userA = Guid.NewGuid();
        var userB = Guid.NewGuid();

        await repo.AddCreditAsync(userA, 100m, null, Guid.NewGuid().ToString());
        await repo.AddCreditAsync(userA, 200m, null, Guid.NewGuid().ToString());
        await repo.AddCreditAsync(userB, 999m, null, Guid.NewGuid().ToString());

        var result = await repo.GetAsync(userA, null, null, null);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(t => t.UserId.Should().Be(userA));
    }

    [Fact]
    public async Task GetAsync_FiltersCorrectlyByType_Credit()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var userId = Guid.NewGuid();

        await repo.AddCreditAsync(userId, 100m, null, Guid.NewGuid().ToString());
        await repo.AddDebitAsync(userId, 50m, null, Guid.NewGuid().ToString());

        var result = await repo.GetAsync(userId, TransactionType.Credit, null, null);

        result.Should().HaveCount(1);
        result[0].Type.Should().Be(TransactionType.Credit);
    }

    [Fact]
    public async Task GetAsync_FiltersCorrectlyByType_Debit()
    {
        using var scope = CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var userId = Guid.NewGuid();

        await repo.AddCreditAsync(userId, 100m, null, Guid.NewGuid().ToString());
        await repo.AddDebitAsync(userId, 50m, null, Guid.NewGuid().ToString());

        var result = await repo.GetAsync(userId, TransactionType.Debit, null, null);

        result.Should().HaveCount(1);
        result[0].Type.Should().Be(TransactionType.Debit);
    }

    [Fact]
    public async Task GetAsync_OrdersByOccurredAtDescending()
    {
        using var scope = CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TransactionDbContext>();
        var repo = scope.ServiceProvider.GetRequiredService<ITransactionRepository>();
        var userId = Guid.NewGuid();

        var older = DateTime.UtcNow.AddDays(-2);
        var newer = DateTime.UtcNow.AddDays(-1);

        db.Transactions.AddRange(
            new TransactionEntry { Id = Guid.NewGuid(), UserId = userId, Amount = 10m, Type = TransactionType.Credit, Description = "", IdempotencyKey = Guid.NewGuid().ToString(), OccurredAt = older },
            new TransactionEntry { Id = Guid.NewGuid(), UserId = userId, Amount = 20m, Type = TransactionType.Credit, Description = "", IdempotencyKey = Guid.NewGuid().ToString(), OccurredAt = newer }
        );
        await db.SaveChangesAsync();

        var result = await repo.GetAsync(userId, null, null, null);

        result.Should().HaveCountGreaterThanOrEqualTo(2);
        // Most recent first
        result[0].OccurredAt.Should().BeAfter(result[^1].OccurredAt);
    }
}
