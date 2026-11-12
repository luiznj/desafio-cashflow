using TransactionEntry = CashFlow.Transaction.Db.Models.Transaction;
using TransactionType = CashFlow.Transaction.Db.Models.TransactionType;

namespace CashFlow.Transaction.Db.Repositories;

public interface ITransactionRepository
{
    // Staging methods: add to change tracker without persisting
    TransactionEntry StageCredit(Guid userId, decimal amount, string? description, string idempotencyKey);
    TransactionEntry StageDebit(Guid userId, decimal amount, string? description, string idempotencyKey);
    void StageOutboxMessage(string type, string payload);
    Task SaveAsync();

    // Legacy helpers (stage + save in one call, used by repository integration tests)
    Task<TransactionEntry> AddCreditAsync(Guid userId, decimal amount, string? description, string idempotencyKey);
    Task<TransactionEntry> AddDebitAsync(Guid userId, decimal amount, string? description, string idempotencyKey);

    Task<TransactionEntry?> FindByIdempotencyKeyAsync(string idempotencyKey);
    Task<IReadOnlyList<TransactionEntry>> GetAsync(Guid userId, TransactionType? type, DateTime? startDate, DateTime? endDate);
}
