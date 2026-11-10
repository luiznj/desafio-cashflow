using CashFlow.Consolidation.Api.Data;
using CashFlow.Consolidation.Api.Models;
using MassTransit;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Api.Messaging;

public record TransactionCreatedEvent(
    Guid TransactionId,
    Guid UserId,
    decimal Amount,
    string Type,
    DateTime OccurredAt);

public class TransactionCreatedConsumer(ConsolidationDbContext db) : IConsumer<TransactionCreatedEvent>
{
    public async Task Consume(ConsumeContext<TransactionCreatedEvent> context)
    {
        var evt = context.Message;
        var date = DateOnly.FromDateTime(evt.OccurredAt);

        var balance = await db.DailyBalances
            .FirstOrDefaultAsync(b => b.UserId == evt.UserId && b.Date == date);

        if (balance is null)
        {
            balance = new DailyBalance { UserId = evt.UserId, Date = date };
            db.DailyBalances.Add(balance);
        }

        if (evt.Type == "Credit")
            balance.TotalCredits += evt.Amount;
        else
            balance.TotalDebits += evt.Amount;

        balance.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();
    }
}
