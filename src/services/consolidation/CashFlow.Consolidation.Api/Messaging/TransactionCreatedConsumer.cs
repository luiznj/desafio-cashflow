using CashFlow.Consolidation.Db.Repositories;
using CashFlow.Shared.Messaging;
using MassTransit;

namespace CashFlow.Consolidation.Api.Messaging;

public class TransactionCreatedConsumer(IDailyBalanceRepository repository) : IConsumer<TransactionCreatedEvent>
{
    public async Task Consume(ConsumeContext<TransactionCreatedEvent> context)
    {
        var evt = context.Message;
        var date = DateOnly.FromDateTime(evt.OccurredAt);

        // signedAmount: positive = credit, negative = debit (as stored by Transaction service)
        await repository.ApplyTransactionAsync(evt.TransactionId, evt.UserId, date, evt.Amount);
    }
}
