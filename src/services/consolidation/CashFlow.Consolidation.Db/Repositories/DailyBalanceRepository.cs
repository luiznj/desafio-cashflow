using CashFlow.Consolidation.Db.Data;
using CashFlow.Consolidation.Db.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Db.Repositories;

public class DailyBalanceRepository(ConsolidationDbContext db) : IDailyBalanceRepository
{
    public async Task ApplyCreditAsync(Guid userId, DateOnly date, decimal amount)
    {
        var balance = await GetOrCreateAsync(userId, date);
        balance.TotalCredits += amount;
        balance.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public async Task ApplyDebitAsync(Guid userId, DateOnly date, decimal amount)
    {
        var balance = await GetOrCreateAsync(userId, date);
        balance.TotalDebits += amount;
        balance.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
    }

    public Task<DailyBalance?> GetByUserAndDateAsync(Guid userId, DateOnly date) =>
        db.DailyBalances.FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);

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
