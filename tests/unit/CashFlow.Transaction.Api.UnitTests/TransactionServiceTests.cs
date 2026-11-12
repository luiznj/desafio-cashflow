using CashFlow.Shared.Messaging;
using CashFlow.Transaction.Api.Dtos;
using CashFlow.Transaction.Api.Services;
using CashFlow.Transaction.Db.Models;
using CashFlow.Transaction.Db.Repositories;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;

namespace CashFlow.Transaction.Api.UnitTests;

public class TransactionServiceTests
{
    private readonly ITransactionRepository _repository = Substitute.For<ITransactionRepository>();
    private readonly IPublishEndpoint _publisher = Substitute.For<IPublishEndpoint>();
    private readonly TransactionService _sut;

    public TransactionServiceTests()
    {
        _sut = new TransactionService(_repository, _publisher, NullLogger<TransactionService>.Instance);
    }

    private static TransactionEntry MakeTransaction(Guid userId, decimal amount, TransactionType type, DateTime? occurredAt = null) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Type = type,
            Description = "",
            IdempotencyKey = Guid.NewGuid().ToString(),
            OccurredAt = occurredAt ?? DateTime.UtcNow
        };

    [Fact]
    public async Task CreateAsync_NewCredit_CallsAddCreditAsync()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns((TransactionEntry?)null);
        _repository.AddCreditAsync(userId, 100m, null, key).Returns(MakeTransaction(userId, 100m, TransactionType.Credit));

        await _sut.CreateAsync(userId, new CreateTransactionRequest(100m, "Credit"), key);

        await _repository.Received(1).AddCreditAsync(userId, 100m, null, key);
        await _repository.DidNotReceive().AddDebitAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_NewDebit_CallsAddDebitAsync()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns((TransactionEntry?)null);
        _repository.AddDebitAsync(userId, 50m, null, key).Returns(MakeTransaction(userId, -50m, TransactionType.Debit));

        await _sut.CreateAsync(userId, new CreateTransactionRequest(50m, "Debit"), key);

        await _repository.Received(1).AddDebitAsync(userId, 50m, null, key);
        await _repository.DidNotReceive().AddCreditAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_NewTransaction_ReturnsIsNewTrue()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns((TransactionEntry?)null);
        _repository.AddCreditAsync(userId, 100m, null, key).Returns(MakeTransaction(userId, 100m, TransactionType.Credit));

        var (_, isNew) = await _sut.CreateAsync(userId, new CreateTransactionRequest(100m, "Credit"), key);

        isNew.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_NewCreditTransaction_PublishesTransactionCreatedEvent()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns((TransactionEntry?)null);
        _repository.AddCreditAsync(userId, 100m, null, key).Returns(MakeTransaction(userId, 100m, TransactionType.Credit));

        await _sut.CreateAsync(userId, new CreateTransactionRequest(100m, "Credit"), key);

        await _publisher.Received(1).Publish(Arg.Any<TransactionCreatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_DuplicateIdempotencyKey_ReturnsIsNewFalse()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns(MakeTransaction(userId, 100m, TransactionType.Credit));

        var (_, isNew) = await _sut.CreateAsync(userId, new CreateTransactionRequest(100m, "Credit"), key);

        isNew.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_DuplicateIdempotencyKey_DoesNotCallAddCreditOrDebit()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns(MakeTransaction(userId, 100m, TransactionType.Credit));

        await _sut.CreateAsync(userId, new CreateTransactionRequest(100m, "Credit"), key);

        await _repository.DidNotReceive().AddCreditAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<string>());
        await _repository.DidNotReceive().AddDebitAsync(Arg.Any<Guid>(), Arg.Any<decimal>(), Arg.Any<string?>(), Arg.Any<string>());
    }

    [Fact]
    public async Task CreateAsync_DuplicateIdempotencyKey_DoesNotPublishEvent()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns(MakeTransaction(userId, 100m, TransactionType.Credit));

        await _sut.CreateAsync(userId, new CreateTransactionRequest(100m, "Credit"), key);

        await _publisher.DidNotReceive().Publish(Arg.Any<TransactionCreatedEvent>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_ResponseAmount_IsAbsoluteValue()
    {
        var userId = Guid.NewGuid();
        var key = Guid.NewGuid().ToString();
        _repository.FindByIdempotencyKeyAsync(key).Returns((TransactionEntry?)null);
        _repository.AddDebitAsync(userId, 50m, null, key).Returns(MakeTransaction(userId, -50m, TransactionType.Debit));

        var (response, _) = await _sut.CreateAsync(userId, new CreateTransactionRequest(50m, "Debit"), key);

        response.Amount.Should().Be(50m);
    }

    [Fact]
    public async Task GetAsync_MultipleTransactionsDifferentDays_GroupsByDateCount()
    {
        var userId = Guid.NewGuid();
        var day1 = new DateTime(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);
        var day2 = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        IReadOnlyList<TransactionEntry> transactions =
        [
            MakeTransaction(userId, 100m, TransactionType.Credit, day1),
            MakeTransaction(userId, 50m, TransactionType.Credit, day1),
            MakeTransaction(userId, 200m, TransactionType.Credit, day2)
        ];
        _repository.GetAsync(userId, Arg.Any<TransactionType?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
            .Returns(Task.FromResult(transactions));

        var result = await _sut.GetAsync(userId, new GetTransactionsQuery { Page = 1, PageSize = 10 });

        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_SameDayTransactions_DailyBalanceSumsSignedAmounts()
    {
        var userId = Guid.NewGuid();
        var day = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        IReadOnlyList<TransactionEntry> transactions =
        [
            MakeTransaction(userId, 100m, TransactionType.Credit, day),
            MakeTransaction(userId, -30m, TransactionType.Debit, day)
        ];
        _repository.GetAsync(userId, Arg.Any<TransactionType?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
            .Returns(Task.FromResult(transactions));

        var result = await _sut.GetAsync(userId, new GetTransactionsQuery { Page = 1, PageSize = 10 });

        result.Data.Should().HaveCount(1);
        result.Data[0].DailyBalance.Should().Be(70m);
    }

    [Fact]
    public async Task GetAsync_ReturnsGroupsOrderedByDateDescending()
    {
        var userId = Guid.NewGuid();
        var older = new DateTime(2026, 11, 9, 12, 0, 0, DateTimeKind.Utc);
        var newer = new DateTime(2026, 11, 11, 12, 0, 0, DateTimeKind.Utc);
        IReadOnlyList<TransactionEntry> transactions =
        [
            MakeTransaction(userId, 100m, TransactionType.Credit, older),
            MakeTransaction(userId, 50m, TransactionType.Credit, newer)
        ];
        _repository.GetAsync(userId, Arg.Any<TransactionType?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
            .Returns(Task.FromResult(transactions));

        var result = await _sut.GetAsync(userId, new GetTransactionsQuery { Page = 1, PageSize = 10 });

        result.Data[0].Date.Should().Be(DateOnly.FromDateTime(newer));
        result.Data[1].Date.Should().Be(DateOnly.FromDateTime(older));
    }

    [Fact]
    public async Task GetAsync_Pagination_Page1PageSize2_Returns2Groups()
    {
        var userId = Guid.NewGuid();
        IReadOnlyList<TransactionEntry> transactions =
        [
            MakeTransaction(userId, 100m, TransactionType.Credit, new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(userId, 100m, TransactionType.Credit, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(userId, 100m, TransactionType.Credit, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc))
        ];
        _repository.GetAsync(userId, Arg.Any<TransactionType?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
            .Returns(Task.FromResult(transactions));

        var result = await _sut.GetAsync(userId, new GetTransactionsQuery { Page = 1, PageSize = 2 });

        result.Data.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAsync_Pagination_TotalPagesCalculatedOnDayGroups()
    {
        var userId = Guid.NewGuid();
        IReadOnlyList<TransactionEntry> transactions =
        [
            MakeTransaction(userId, 100m, TransactionType.Credit, new DateTime(2026, 11, 9, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(userId, 100m, TransactionType.Credit, new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc)),
            MakeTransaction(userId, 100m, TransactionType.Credit, new DateTime(2026, 11, 11, 0, 0, 0, DateTimeKind.Utc))
        ];
        _repository.GetAsync(userId, Arg.Any<TransactionType?>(), Arg.Any<DateTime?>(), Arg.Any<DateTime?>())
            .Returns(Task.FromResult(transactions));

        var result = await _sut.GetAsync(userId, new GetTransactionsQuery { Page = 1, PageSize = 2 });

        result.TotalItems.Should().Be(3);
        result.TotalPages.Should().Be(2);
    }
}
