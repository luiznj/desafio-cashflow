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

        if (evt.Type == "Credit")
            await repository.ApplyCreditAsync(evt.UserId, date, evt.Amount);
        else
            await repository.ApplyDebitAsync(evt.UserId, date, evt.Amount);
    }
}
