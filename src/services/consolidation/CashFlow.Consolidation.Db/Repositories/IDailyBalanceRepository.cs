using CashFlow.Consolidation.Db.Models;

namespace CashFlow.Consolidation.Db.Repositories;

public interface IDailyBalanceRepository
{
    Task ApplyTransactionAsync(Guid transactionId, Guid userId, DateOnly date, decimal signedAmount);
    Task<DailyBalance?> GetByUserAndDateAsync(Guid userId, DateOnly date);
}
