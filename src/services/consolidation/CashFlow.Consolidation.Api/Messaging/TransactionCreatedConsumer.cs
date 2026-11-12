using CashFlow.Consolidation.Db.Repositories;
using CashFlow.Shared.Messaging;
using MassTransit;

namespace CashFlow.Consolidation.Api.Messaging;

public class TransactionCreatedConsumer(IDailyBalanceRepository repository, ILogger<TransactionCreatedConsumer> logger) : IConsumer<TransactionCreatedEvent>
{
    public async Task Consume(ConsumeContext<TransactionCreatedEvent> context)
    {
        var evt = context.Message;
        var date = DateOnly.FromDateTime(evt.OccurredAt);

        logger.LogInformation("Evento recebido: TransactionId {TransactionId} | UserId {UserId} | Data {Date} | Valor {Amount}", evt.TransactionId, evt.UserId, date, evt.Amount);

        // signedAmount: positive = credit, negative = debit (as stored by Transaction service)
        await repository.ApplyTransactionAsync(evt.TransactionId, evt.UserId, date, evt.Amount);

        logger.LogInformation("Saldo consolidado atualizado: TransactionId {TransactionId}", evt.TransactionId);
    }
}
