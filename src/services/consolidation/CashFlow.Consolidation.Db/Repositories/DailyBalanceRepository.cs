using CashFlow.Consolidation.Db.Data;
using CashFlow.Consolidation.Db.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Db.Repositories;

public class DailyBalanceRepository(ConsolidationDbContext db) : IDailyBalanceRepository
{
    public async Task ApplyTransactionAsync(Guid transactionId, Guid userId, DateOnly date, decimal signedAmount)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        var alreadyProcessed = await db.ProcessedTransactions
            .AnyAsync(p => p.TransactionId == transactionId);

        if (alreadyProcessed)
            return; // tx disposed without commit → auto-rollback (no-op)

        var balance = await GetOrCreateAsync(userId, date);

        if (signedAmount > 0)
            balance.TotalCredits += signedAmount;
        else
            balance.TotalDebits += Math.Abs(signedAmount);

        balance.UpdatedAt = DateTime.UtcNow;

        db.ProcessedTransactions.Add(new ProcessedTransaction
        {
            TransactionId = transactionId,
            ProcessedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();
    }

    public Task<DailyBalance?> GetByUserAndDateAsync(Guid userId, DateOnly date) =>
        db.DailyBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);

    private async Task<DailyBalance> GetOrCreateAsync(Guid userId, DateOnly date)
    {
        var balance = await db.DailyBalances
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);

        if (balance is null)
        {
            balance = new DailyBalance { Id = Guid.NewGuid(), UserId = userId, Date = date };
            db.DailyBalances.Add(balance);
        }

        return balance;
    }
}
