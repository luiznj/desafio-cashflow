using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;
using TransactionType = CashFlow.Transaction.Db.Models.TransactionType;

namespace CashFlow.Transaction.Db.Repositories;

public interface ITransactionRepository
{
    Task<TransactionEntry> AddCreditAsync(Guid userId, decimal amount, string? description, string idempotencyKey);
    Task<TransactionEntry> AddDebitAsync(Guid userId, decimal amount, string? description, string idempotencyKey);
    Task<TransactionEntry?> FindByIdempotencyKeyAsync(string idempotencyKey);
    Task<IReadOnlyList<TransactionEntry>> GetAsync(Guid userId, TransactionType? type, DateTime? startDate, DateTime? endDate);
}
