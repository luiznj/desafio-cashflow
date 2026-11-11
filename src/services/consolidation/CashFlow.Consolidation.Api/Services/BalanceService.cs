using CashFlow.Consolidation.Api.Dtos;
using CashFlow.Consolidation.Db.Repositories;

namespace CashFlow.Consolidation.Api.Services;

public class BalanceService(IDailyBalanceRepository repository) : IBalanceService
{
    public async Task<DailyBalanceResponse> GetAsync(Guid userId, DateOnly date)
    {
        var balance = await repository.GetByUserAndDateAsync(userId, date);

        if (balance is null)
            return new DailyBalanceResponse(date, 0m, 0m, 0m);

        return new DailyBalanceResponse(date, balance.TotalCredits, balance.TotalDebits, balance.Balance);
    }
}
