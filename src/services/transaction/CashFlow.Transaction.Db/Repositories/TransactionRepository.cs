using CashFlow.Transaction.Db.Data;
using Microsoft.EntityFrameworkCore;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;
using TransactionType = CashFlow.Transaction.Db.Models.TransactionType;

namespace CashFlow.Transaction.Db.Repositories;

public class TransactionRepository(TransactionDbContext db) : ITransactionRepository
{
    public Task<TransactionEntry> AddCreditAsync(Guid userId, decimal amount, string? description, string idempotencyKey) =>
        AddAsync(userId, amount, TransactionType.Credit, description, idempotencyKey);

    public Task<TransactionEntry> AddDebitAsync(Guid userId, decimal amount, string? description, string idempotencyKey) =>
        AddAsync(userId, -amount, TransactionType.Debit, description, idempotencyKey);

    public Task<TransactionEntry?> FindByIdempotencyKeyAsync(string idempotencyKey) =>
        db.Transactions.FirstOrDefaultAsync(t => t.IdempotencyKey == idempotencyKey);

    public async Task<IReadOnlyList<TransactionEntry>> GetAsync(Guid userId, TransactionType? type, DateTime? startDate, DateTime? endDate)
    {
        var query = db.Transactions.Where(t => t.UserId == userId);

        if (type.HasValue)
            query = query.Where(t => t.Type == type.Value);

        if (startDate.HasValue)
            query = query.Where(t => t.OccurredAt >= startDate.Value);

        if (endDate.HasValue)
            query = query.Where(t => t.OccurredAt <= endDate.Value);

        return await query.OrderByDescending(t => t.OccurredAt).ToListAsync();
    }

    private async Task<TransactionEntry> AddAsync(Guid userId, decimal amount, TransactionType type, string? description, string idempotencyKey)
    {
        var entry = new TransactionEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Type = type,
            Description = description ?? string.Empty,
            IdempotencyKey = idempotencyKey,
            OccurredAt = DateTime.UtcNow
        };
        db.Transactions.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }
}
