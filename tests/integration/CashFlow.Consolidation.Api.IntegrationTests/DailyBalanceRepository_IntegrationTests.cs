using CashFlow.Consolidation.Db.Data;
using CashFlow.Consolidation.Db.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CashFlow.Consolidation.Api.IntegrationTests;

public class DailyBalanceRepository_IntegrationTests : IClassFixture<ConsolidationApiFactory>
{
    private readonly ConsolidationApiFactory _factory;

    public DailyBalanceRepository_IntegrationTests(ConsolidationApiFactory factory)
    {
        _factory = factory;
    }

    private IDailyBalanceRepository GetRepo(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<IDailyBalanceRepository>();

    private ConsolidationDbContext GetDb(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();

    [Fact]
    public async Task ApplyTransaction_Credit_IncreasesTotalCredits()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 1);

        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, 100m);

        var balance = await GetDb(scope).DailyBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);
        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(100m);
        balance.TotalDebits.Should().Be(0m);
    }

    [Fact]
    public async Task ApplyTransaction_Debit_IncreasesTotalDebits()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 2);

        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, -50m);

        var balance = await GetDb(scope).DailyBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);
        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(0m);
        balance.TotalDebits.Should().Be(50m);
    }

    [Fact]
    public async Task ApplyTransaction_BalanceIsCreditsMinusDebits()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 3);

        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, 100m);
        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, -30m);

        var balance = await GetDb(scope).DailyBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);
        balance.Should().NotBeNull();
        balance!.Balance.Should().Be(70m);
    }

    [Fact]
    public async Task ApplyTransaction_CreatesNewDailyBalanceWhenNotExists()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 4);

        var before = await GetDb(scope).DailyBalances.AsNoTracking()
            .AnyAsync(b => b.UserId == userId && b.Date == date);
        before.Should().BeFalse();

        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, 50m);

        var after = await GetDb(scope).DailyBalances.AsNoTracking()
            .AnyAsync(b => b.UserId == userId && b.Date == date);
        after.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyTransaction_UpsertExistingRecord_AccumulatesCorrectly()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 5);

        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, 100m);
        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, 50m);

        var balance = await GetDb(scope).DailyBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);
        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(150m);
    }

    [Fact]
    public async Task ApplyTransaction_SameTransactionIdTwice_IsIdempotent()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 6);
        var transactionId = Guid.NewGuid();

        await repo.ApplyTransactionAsync(transactionId, userId, date, 100m);
        await repo.ApplyTransactionAsync(transactionId, userId, date, 100m); // duplicate

        var balance = await GetDb(scope).DailyBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);
        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(100m); // not 200
    }

    [Fact]
    public async Task ApplyTransaction_MultipleTransactionsDifferentDates_SeparateRows()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date1 = new DateOnly(2026, 11, 7);
        var date2 = new DateOnly(2026, 11, 8);

        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date1, 100m);
        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date2, 200m);

        var balances = await GetDb(scope).DailyBalances.AsNoTracking()
            .Where(b => b.UserId == userId)
            .ToListAsync();
        balances.Should().HaveCount(2);
        balances.Single(b => b.Date == date1).TotalCredits.Should().Be(100m);
        balances.Single(b => b.Date == date2).TotalCredits.Should().Be(200m);
    }

    [Fact]
    public async Task GetByUserAndDateAsync_ExistingRecord_ReturnsIt()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 9);

        await repo.ApplyTransactionAsync(Guid.NewGuid(), userId, date, 75m);

        var result = await repo.GetByUserAndDateAsync(userId, date);

        result.Should().NotBeNull();
        result!.TotalCredits.Should().Be(75m);
    }

    [Fact]
    public async Task GetByUserAndDateAsync_NoRecord_ReturnsNull()
    {
        using var scope = _factory.Services.CreateScope();
        var repo = GetRepo(scope);
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 10);

        var result = await repo.GetByUserAndDateAsync(userId, date);

        result.Should().BeNull();
    }
}
