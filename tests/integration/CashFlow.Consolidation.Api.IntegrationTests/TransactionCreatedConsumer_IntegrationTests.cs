using CashFlow.Consolidation.Db.Data;
using CashFlow.Shared.Messaging;
using FluentAssertions;
using MassTransit.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CashFlow.Consolidation.Api.IntegrationTests;

[Collection("ConsolidationApi")]
public class TransactionCreatedConsumer_IntegrationTests
{
    private readonly ConsolidationApiFactory _factory;
    private readonly ITestHarness _harness;

    public TransactionCreatedConsumer_IntegrationTests(ConsolidationApiFactory factory)
    {
        _factory = factory;
        _harness = factory.Services.GetRequiredService<ITestHarness>();
    }

    private async Task<CashFlow.Consolidation.Db.Models.DailyBalance?> GetBalanceAsync(Guid userId, DateOnly date)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ConsolidationDbContext>();
        return await db.DailyBalances.AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);
    }

    private async Task WaitForConsumptionAsync(int expectedCount = 1)
    {
        // Poll until consumed or timeout
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (DateTime.UtcNow < deadline)
        {
            var consumed = await _harness.Consumed.SelectAsync<TransactionCreatedEvent>().ToListAsync();
            if (consumed.Count >= expectedCount)
                return;
            await Task.Delay(100);
        }
    }

    [Fact]
    public async Task ConsumeEvent_CreditEvent_UpdatesDailyBalance()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 20);
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), userId, 150m, "Credit",
            new DateTime(date.Year, date.Month, date.Day, 10, 0, 0, DateTimeKind.Utc));

        await _harness.Bus.Publish(evt);
        await WaitForConsumptionAsync();

        var balance = await GetBalanceAsync(userId, date);
        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(150m);
        balance.TotalDebits.Should().Be(0m);
    }

    [Fact]
    public async Task ConsumeEvent_DebitEvent_UpdatesDailyBalance()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 21);
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), userId, -75m, "Debit",
            new DateTime(date.Year, date.Month, date.Day, 10, 0, 0, DateTimeKind.Utc));

        await _harness.Bus.Publish(evt);
        await WaitForConsumptionAsync();

        var balance = await GetBalanceAsync(userId, date);
        balance.Should().NotBeNull();
        balance!.TotalDebits.Should().Be(75m);
        balance.TotalCredits.Should().Be(0m);
    }

    [Fact]
    public async Task ConsumeEvent_DuplicateTransactionId_DoesNotDoubleCount()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 22);
        var transactionId = Guid.NewGuid();
        var evt = new TransactionCreatedEvent(
            transactionId, userId, 100m, "Credit",
            new DateTime(date.Year, date.Month, date.Day, 10, 0, 0, DateTimeKind.Utc));

        await _harness.Bus.Publish(evt);
        await WaitForConsumptionAsync(1);

        await _harness.Bus.Publish(evt); // duplicate
        await WaitForConsumptionAsync(2);

        var balance = await GetBalanceAsync(userId, date);
        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(100m); // not 200
    }

    [Fact]
    public async Task ConsumeEvent_CreatesBalanceIfNotExists()
    {
        var userId = Guid.NewGuid();
        var date = new DateOnly(2026, 11, 23);
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), userId, 50m, "Credit",
            new DateTime(date.Year, date.Month, date.Day, 10, 0, 0, DateTimeKind.Utc));

        var before = await GetBalanceAsync(userId, date);
        before.Should().BeNull();

        await _harness.Bus.Publish(evt);
        await WaitForConsumptionAsync();

        var after = await GetBalanceAsync(userId, date);
        after.Should().NotBeNull();
    }

    [Fact]
    public async Task ConsumeEvent_CorrectDateExtractedFromOccurredAt()
    {
        var userId = Guid.NewGuid();
        var expectedDate = new DateOnly(2026, 11, 24);
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), userId, 100m, "Credit",
            new DateTime(2026, 11, 24, 23, 59, 59, DateTimeKind.Utc));

        await _harness.Bus.Publish(evt);
        await WaitForConsumptionAsync();

        var balance = await GetBalanceAsync(userId, expectedDate);
        balance.Should().NotBeNull();
        balance!.TotalCredits.Should().Be(100m);
    }
}
