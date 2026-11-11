using CashFlow.Consolidation.Db.Models;

namespace CashFlow.Consolidation.Db.Repositories;

public interface IDailyBalanceRepository
{
    Task ApplyCreditAsync(Guid userId, DateOnly date, decimal amount);
    Task ApplyDebitAsync(Guid userId, DateOnly date, decimal amount);
    Task<DailyBalance?> GetByUserAndDateAsync(Guid userId, DateOnly date);
}
