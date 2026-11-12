using CashFlow.Consolidation.Api.Messaging;
using CashFlow.Consolidation.Db.Repositories;
using CashFlow.Shared.Messaging;
using FluentAssertions;
using MassTransit;
using NSubstitute;
using Xunit;

namespace CashFlow.Consolidation.Api.UnitTests;

public class TransactionCreatedConsumerTests
{
    private readonly IDailyBalanceRepository _repository = Substitute.For<IDailyBalanceRepository>();
    private readonly TransactionCreatedConsumer _sut;

    public TransactionCreatedConsumerTests()
    {
        _sut = new TransactionCreatedConsumer(_repository);
    }

    private static ConsumeContext<TransactionCreatedEvent> BuildContext(TransactionCreatedEvent evt)
    {
        var context = Substitute.For<ConsumeContext<TransactionCreatedEvent>>();
        context.Message.Returns(evt);
        return context;
    }

    [Fact]
    public async Task Consume_CreditEvent_CallsApplyTransactionWithPositiveAmount()
    {
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), 100m, "Credit",
            new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc));

        await _sut.Consume(BuildContext(evt));

        await _repository.Received(1).ApplyTransactionAsync(
            evt.TransactionId, evt.UserId,
            Arg.Any<DateOnly>(),
            100m);
    }

    [Fact]
    public async Task Consume_DebitEvent_CallsApplyTransactionWithNegativeAmount()
    {
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), -50m, "Debit",
            new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc));

        await _sut.Consume(BuildContext(evt));

        await _repository.Received(1).ApplyTransactionAsync(
            evt.TransactionId, evt.UserId,
            Arg.Any<DateOnly>(),
            -50m);
    }

    [Fact]
    public async Task Consume_Event_ExtractsDateFromOccurredAt()
    {
        var occurredAt = new DateTime(2026, 11, 11, 23, 59, 59, DateTimeKind.Utc);
        var expectedDate = new DateOnly(2026, 11, 11);
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), Guid.NewGuid(), 100m, "Credit", occurredAt);

        await _sut.Consume(BuildContext(evt));

        await _repository.Received(1).ApplyTransactionAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(),
            expectedDate,
            Arg.Any<decimal>());
    }

    [Fact]
    public async Task Consume_Event_PassesTransactionIdCorrectly()
    {
        var transactionId = Guid.NewGuid();
        var evt = new TransactionCreatedEvent(
            transactionId, Guid.NewGuid(), 100m, "Credit",
            new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc));

        await _sut.Consume(BuildContext(evt));

        await _repository.Received(1).ApplyTransactionAsync(
            transactionId, Arg.Any<Guid>(),
            Arg.Any<DateOnly>(), Arg.Any<decimal>());
    }

    [Fact]
    public async Task Consume_Event_PassesUserIdCorrectly()
    {
        var userId = Guid.NewGuid();
        var evt = new TransactionCreatedEvent(
            Guid.NewGuid(), userId, 100m, "Credit",
            new DateTime(2026, 11, 11, 10, 0, 0, DateTimeKind.Utc));

        await _sut.Consume(BuildContext(evt));

        await _repository.Received(1).ApplyTransactionAsync(
            Arg.Any<Guid>(), userId,
            Arg.Any<DateOnly>(), Arg.Any<decimal>());
    }
}
