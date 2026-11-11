using CashFlow.Transaction.Db.Data;
using Microsoft.EntityFrameworkCore;
using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;

namespace CashFlow.Transaction.Db.Repositories;

public class TransactionRepository(TransactionDbContext db) : ITransactionRepository
{
    public Task AddCreditAsync(Guid userId, decimal amount, string description, DateTime occurredAt) =>
        AddAsync(userId, amount, Models.TransactionType.Credit, description, occurredAt);

    public Task AddDebitAsync(Guid userId, decimal amount, string description, DateTime occurredAt) =>
        AddAsync(userId, amount, Models.TransactionType.Debit, description, occurredAt);

    private async Task AddAsync(Guid userId, decimal amount, Models.TransactionType type, string description, DateTime occurredAt)
    {
        db.Transactions.Add(new TransactionEntry
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Amount = amount,
            Type = type,
            Description = description,
            OccurredAt = occurredAt
        });
        await db.SaveChangesAsync();
    }
}
