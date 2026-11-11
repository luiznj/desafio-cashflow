namespace CashFlow.Transaction.Db.Repositories;

public interface ITransactionRepository
{
    Task AddCreditAsync(Guid userId, decimal amount, string description, DateTime occurredAt);
    Task AddDebitAsync(Guid userId, decimal amount, string description, DateTime occurredAt);
}
