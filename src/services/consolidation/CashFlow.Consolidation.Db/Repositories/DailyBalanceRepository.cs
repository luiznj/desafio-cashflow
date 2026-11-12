using CashFlow.Consolidation.Db.Data;
using CashFlow.Consolidation.Db.Models;
using Microsoft.EntityFrameworkCore;

namespace CashFlow.Consolidation.Db.Repositories;

public class DailyBalanceRepository(ConsolidationDbContext db) : IDailyBalanceRepository
{
    public async Task ApplyTransactionAsync(Guid transactionId, Guid userId, DateOnly date, decimal signedAmount)
    {
        await using var tx = await db.Database.BeginTransactionAsync();

        // Idempotency: claim this transaction atomically; if already claimed, skip
        var claimed = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO consolidation."ProcessedTransactions" ("TransactionId", "ProcessedAt")
            VALUES ({transactionId}, {DateTime.UtcNow})
            ON CONFLICT ("TransactionId") DO NOTHING
            """);

        if (claimed == 0)
            return;

        var credits = signedAmount > 0 ? signedAmount : 0m;
        var debits = signedAmount < 0 ? Math.Abs(signedAmount) : 0m;

        // Atomic upsert: no lost-update risk even with concurrent messages for the same user/day
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO consolidation."DailyBalances" ("Id", "UserId", "Date", "TotalCredits", "TotalDebits", "UpdatedAt")
            VALUES ({Guid.NewGuid()}, {userId}, {date}, {credits}, {debits}, {DateTime.UtcNow})
            ON CONFLICT ("UserId", "Date")
            DO UPDATE SET
                "TotalCredits" = consolidation."DailyBalances"."TotalCredits" + EXCLUDED."TotalCredits",
                "TotalDebits"  = consolidation."DailyBalances"."TotalDebits"  + EXCLUDED."TotalDebits",
                "UpdatedAt"    = EXCLUDED."UpdatedAt"
            """);

        await tx.CommitAsync();
    }

    public Task<DailyBalance?> GetByUserAndDateAsync(Guid userId, DateOnly date) =>
        db.DailyBalances
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.UserId == userId && b.Date == date);
}
